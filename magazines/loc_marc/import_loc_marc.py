from __future__ import annotations

import argparse
import csv
import json
import re
import sys
import time
import urllib.parse
import urllib.request
import xml.etree.ElementTree as ET
from pathlib import Path
from typing import Iterable


DEFAULT_PROJECT_ROOT = Path(r"P:\Git\readarr\magazines\loc_marc")
DEFAULT_RAW_DIR = DEFAULT_PROJECT_ROOT / "raw"
DEFAULT_OUTPUT_CSV = DEFAULT_PROJECT_ROOT / "loc_magazine_titles_seed.csv"

LOC_SEARCH_URL = "https://www.loc.gov/books/"
LCCN_MARCXML_URL_TEMPLATE = "https://lccn.loc.gov/{lccn}/marcxml"

USER_AGENT = "readarr-magazine-metadata-research/0.1"


# -----------------------------
# General helpers
# -----------------------------

def clean_text(value: str | None) -> str:
    if not value:
        return ""

    value = re.sub(r"\s+", " ", value).strip()
    value = re.sub(r"\s*[/,:;.]$", "", value).strip()
    return value


def normalize_key(value: str | None) -> str:
    if not value:
        return ""

    value = value.lower()
    value = value.replace("&", " and ")
    value = re.sub(r"[^a-z0-9]+", " ", value)
    value = re.sub(r"\s+", " ", value).strip()
    return value


def unique_join(values: Iterable[str]) -> str:
    cleaned = sorted({clean_text(v) for v in values if clean_text(v)})
    return " | ".join(cleaned)


def safe_filename(value: str) -> str:
    value = re.sub(r"[^a-zA-Z0-9_.-]+", "_", value)
    value = value.strip("._")
    return value or "record"


def http_get_text(url: str, timeout: int = 60) -> str:
    request = urllib.request.Request(
        url,
        headers={
            "User-Agent": USER_AGENT,
            "Accept": "application/json,text/xml,application/xml,text/plain,*/*",
        },
    )

    with urllib.request.urlopen(request, timeout=timeout) as response:
        data = response.read()

    return data.decode("utf-8", errors="replace")


def http_get_bytes(url: str, timeout: int = 60) -> bytes:
    request = urllib.request.Request(
        url,
        headers={
            "User-Agent": USER_AGENT,
            "Accept": "text/xml,application/xml,*/*",
        },
    )

    with urllib.request.urlopen(request, timeout=timeout) as response:
        return response.read()


# -----------------------------
# LOC downloader
# -----------------------------

def build_loc_search_url(query: str, page: int, page_count: int) -> str:
    params = {
        "fo": "json",
        "c": str(page_count),
        "sp": str(page),
        "q": query,
        "fa": "partof:catalog",
    }

    return f"{LOC_SEARCH_URL}?{urllib.parse.urlencode(params)}"


def extract_lccns_from_result(result: dict) -> list[str]:
    candidates: list[str] = []

    for key in (
        "number_lccn",
        "lccn",
        "control_number",
        "number",
    ):
        value = result.get(key)

        if isinstance(value, str):
            candidates.append(value)

        elif isinstance(value, list):
            candidates.extend(str(item) for item in value if item)

    # LOC JSON often buries identifiers in "number_*" lists.
    for key, value in result.items():
        if "lccn" not in key.lower():
            continue

        if isinstance(value, str):
            candidates.append(value)
        elif isinstance(value, list):
            candidates.extend(str(item) for item in value if item)

    lccns: list[str] = []

    for candidate in candidates:
        # LCCNs can look like sn83045160, 2001234567, etc.
        for match in re.findall(r"\b[a-z]{0,3}\d{6,12}\b", candidate.lower()):
            lccns.append(match)

    return sorted(set(lccns))


def looks_like_marcxml(data: bytes) -> bool:
    head = data[:500].decode("utf-8", errors="ignore").lower()
    return "<record" in head or "<collection" in head or "marc21/slim" in head


def download_marcxml_by_lccn(lccn: str, raw_dir: Path, overwrite: bool) -> Path | None:
    output_path = raw_dir / f"{safe_filename(lccn)}.marcxml"

    if output_path.exists() and not overwrite:
        return output_path

    url = LCCN_MARCXML_URL_TEMPLATE.format(lccn=urllib.parse.quote(lccn))

    try:
        data = http_get_bytes(url)

        if not looks_like_marcxml(data):
            print(f"WARN not MARCXML: {lccn} :: {url}", file=sys.stderr)
            return None

        output_path.write_bytes(data)
        return output_path

    except Exception as exc:
        print(f"WARN failed MARCXML download: {lccn} :: {exc}", file=sys.stderr)
        return None


def download_loc_records(
    raw_dir: Path,
    queries: list[str],
    max_pages: int,
    page_count: int,
    sleep_seconds: float,
    overwrite: bool,
) -> int:
    raw_dir.mkdir(parents=True, exist_ok=True)

    downloaded = 0
    seen_lccns: set[str] = set()

    for query in queries:
        print(f"LOC search query: {query}")

        for page in range(1, max_pages + 1):
            url = build_loc_search_url(
                query=query,
                page=page,
                page_count=page_count,
            )

            print(f"Search page {page}: {url}")

            try:
                payload = json.loads(http_get_text(url))
            except Exception as exc:
                print(f"ERROR LOC search failed: {exc}", file=sys.stderr)
                break

            results = payload.get("results") or []

            if not results:
                print("No more results.")
                break

            page_lccns: list[str] = []

            for result in results:
                page_lccns.extend(extract_lccns_from_result(result))

            page_lccns = sorted(set(page_lccns))
            print(f"LCCNs found on page: {len(page_lccns)}")

            for lccn in page_lccns:
                if lccn in seen_lccns:
                    continue

                seen_lccns.add(lccn)

                path = download_marcxml_by_lccn(
                    lccn=lccn,
                    raw_dir=raw_dir,
                    overwrite=overwrite,
                )

                if path:
                    downloaded += 1
                    print(f"Downloaded: {path.name}")

                time.sleep(sleep_seconds)

            time.sleep(sleep_seconds)

    print(f"Downloaded MARCXML files: {downloaded}")
    return downloaded


# -----------------------------
# MARCXML parser
# -----------------------------

def local_name(tag: str) -> str:
    return tag.split("}", 1)[-1] if "}" in tag else tag


def get_controlfield(record: ET.Element, tag: str) -> str:
    # Leader is an XML element, not a controlfield.
    if tag == "000":
        for child in record:
            if local_name(child.tag) == "leader":
                return clean_text(child.text)

    for field in record:
        if local_name(field.tag) == "controlfield" and field.attrib.get("tag") == tag:
            return clean_text(field.text)

    return ""


def get_datafields(record: ET.Element, tag: str) -> list[ET.Element]:
    return [
        field
        for field in record
        if local_name(field.tag) == "datafield" and field.attrib.get("tag") == tag
    ]


def get_subfields(datafield: ET.Element | None, codes: set[str]) -> list[str]:
    if datafield is None:
        return []

    values: list[str] = []

    for subfield in datafield:
        if local_name(subfield.tag) != "subfield":
            continue

        if subfield.attrib.get("code") in codes:
            value = clean_text(subfield.text)
            if value:
                values.append(value)

    return values


def first_subfield(datafield: ET.Element | None, codes: set[str]) -> str:
    values = get_subfields(datafield, codes)
    return values[0] if values else ""


def get_title(record: ET.Element) -> str:
    fields = get_datafields(record, "245")
    if not fields:
        return ""

    parts = get_subfields(fields[0], {"a", "b"})
    return clean_text(" ".join(parts))


def get_variant_titles(record: ET.Element) -> str:
    variants: list[str] = []

    for tag in ("210", "222", "240", "242", "246", "247"):
        for field in get_datafields(record, tag):
            variants.extend(get_subfields(field, {"a", "b"}))

    return unique_join(variants)


def get_lccn(record: ET.Element) -> str:
    fields = get_datafields(record, "010")
    if not fields:
        return ""

    value = first_subfield(fields[0], {"a"})
    return re.sub(r"\s+", "", value)


def get_issns(record: ET.Element) -> str:
    values: list[str] = []

    for field in get_datafields(record, "022"):
        values.extend(get_subfields(field, {"a", "l", "m", "y", "z"}))

    normalized = []

    for value in values:
        issn = re.sub(r"[^0-9Xx-]", "", value).upper()
        if issn:
            normalized.append(issn)

    return unique_join(normalized)


def get_publisher(record: ET.Element) -> str:
    for tag in ("264", "260"):
        fields = get_datafields(record, tag)
        if fields:
            publisher = first_subfield(fields[0], {"b"})
            if publisher:
                return publisher

    return ""


def get_publication_place(record: ET.Element) -> str:
    for tag in ("264", "260"):
        fields = get_datafields(record, tag)
        if fields:
            place = first_subfield(fields[0], {"a"})
            if place:
                return place

    return ""


def get_frequency(record: ET.Element) -> str:
    current: list[str] = []
    former: list[str] = []

    for field in get_datafields(record, "310"):
        current.extend(get_subfields(field, {"a", "b"}))

    for field in get_datafields(record, "321"):
        former.extend(get_subfields(field, {"a", "b"}))

    current_text = unique_join(current)
    former_text = unique_join(former)

    if current_text and former_text:
        return f"{current_text} | former: {former_text}"

    return current_text or former_text


def get_publication_dates(record: ET.Element) -> str:
    values: list[str] = []

    for field in get_datafields(record, "362"):
        values.extend(get_subfields(field, {"a"}))

    return unique_join(values)


def guess_start_year(publication_dates: str) -> str:
    if not publication_dates:
        return ""

    match = re.search(r"(18|19|20)\d{2}", publication_dates)
    return match.group(0) if match else ""


def get_subjects(record: ET.Element) -> str:
    values: list[str] = []

    for tag in ("650", "655"):
        for field in get_datafields(record, tag):
            values.extend(get_subfields(field, {"a", "v", "x", "y", "z"}))

    return unique_join(values)


def get_genres(record: ET.Element) -> str:
    values: list[str] = []

    for field in get_datafields(record, "655"):
        values.extend(get_subfields(field, {"a", "v"}))

    return unique_join(values)


def get_languages(record: ET.Element) -> str:
    values: list[str] = []

    for field in get_datafields(record, "041"):
        values.extend(get_subfields(field, {"a", "d", "e", "h"}))

    field_008 = get_controlfield(record, "008")
    if len(field_008) >= 38:
        lang = field_008[35:38].strip()
        if lang:
            values.append(lang)

    return unique_join(values)


def is_likely_serial(record: ET.Element) -> bool:
    leader = get_controlfield(record, "000")

    # MARC leader position 7:
    # s = serial
    # i = integrating resource
    if len(leader) > 7 and leader[7] in {"s", "i"}:
        return True

    if get_datafields(record, "310"):
        return True

    if get_datafields(record, "362"):
        return True

    if get_datafields(record, "022"):
        return True

    return False


def parse_record(record: ET.Element, source_file: Path) -> dict[str, str] | None:
    if not is_likely_serial(record):
        return None

    title = get_title(record)
    if not title:
        return None

    publication_dates = get_publication_dates(record)

    return {
        "source": "loc_marcxml",
        "source_file": source_file.name,
        "record_control_number": get_controlfield(record, "001"),
        "lccn": get_lccn(record),
        "issn": get_issns(record),
        "canonical_title": title,
        "normalized_title": normalize_key(title),
        "variant_titles": get_variant_titles(record),
        "publisher": get_publisher(record),
        "publication_place": get_publication_place(record),
        "language": get_languages(record),
        "frequency": get_frequency(record),
        "publication_dates": publication_dates,
        "start_year_guess": guess_start_year(publication_dates),
        "genres": get_genres(record),
        "subjects": get_subjects(record),
    }


def iter_marcxml_records(path: Path) -> Iterable[ET.Element]:
    context = ET.iterparse(path, events=("end",))

    for _, elem in context:
        if local_name(elem.tag) == "record":
            yield elem
            elem.clear()


def find_input_files(input_dir: Path) -> list[Path]:
    patterns = ("*.xml", "*.marcxml")
    files: list[Path] = []

    for pattern in patterns:
        files.extend(input_dir.rglob(pattern))

    return sorted(set(files))


def write_csv(rows: Iterable[dict[str, str]], output_csv: Path) -> int:
    fieldnames = [
        "source",
        "source_file",
        "record_control_number",
        "lccn",
        "issn",
        "canonical_title",
        "normalized_title",
        "variant_titles",
        "publisher",
        "publication_place",
        "language",
        "frequency",
        "publication_dates",
        "start_year_guess",
        "genres",
        "subjects",
    ]

    output_csv.parent.mkdir(parents=True, exist_ok=True)

    count = 0

    with output_csv.open("w", newline="", encoding="utf-8") as handle:
        writer = csv.DictWriter(handle, fieldnames=fieldnames)
        writer.writeheader()

        for row in rows:
            writer.writerow(row)
            count += 1

    return count


def build_rows(input_dir: Path, max_files: int, max_records: int) -> Iterable[dict[str, str]]:
    files = find_input_files(input_dir)

    if max_files > 0:
        files = files[:max_files]

    print(f"Input dir: {input_dir}")
    print(f"Files found: {len(files)}")

    total_seen = 0
    total_kept = 0

    for file_path in files:
        print(f"Parsing: {file_path}")

        try:
            for record in iter_marcxml_records(file_path):
                total_seen += 1

                row = parse_record(record, file_path)
                if row is None:
                    continue

                total_kept += 1
                yield row

                if max_records > 0 and total_kept >= max_records:
                    print(f"Record cap hit: {max_records}")
                    print(f"Records seen: {total_seen}")
                    print(f"Rows kept: {total_kept}")
                    return

        except ET.ParseError as exc:
            print(f"ERROR parsing XML: {file_path} :: {exc}", file=sys.stderr)

    print(f"Records seen: {total_seen}")
    print(f"Rows kept: {total_kept}")


# -----------------------------
# CLI
# -----------------------------

def parse_args() -> argparse.Namespace:
    parser = argparse.ArgumentParser(
        description="Download LOC MARCXML records and import serial/magazine records into CSV."
    )

    parser.add_argument(
        "--raw-dir",
        default=DEFAULT_RAW_DIR,
        type=Path,
        help="Folder to store downloaded MARCXML files.",
    )

    parser.add_argument(
        "--input-dir",
        default=DEFAULT_RAW_DIR,
        type=Path,
        help="Folder containing MARCXML files to parse.",
    )

    parser.add_argument(
        "--output-csv",
        default=DEFAULT_OUTPUT_CSV,
        type=Path,
        help="Output CSV path.",
    )

    parser.add_argument(
        "--download",
        action="store_true",
        help="Download LOC MARCXML files before parsing.",
    )

    parser.add_argument(
        "--query",
        action="append",
        default=[],
        help="LOC search query. Can be repeated.",
    )

    parser.add_argument(
        "--max-pages",
        default=5,
        type=int,
        help="Max LOC search pages per query.",
    )

    parser.add_argument(
        "--page-count",
        default=100,
        type=int,
        help="LOC results per search page.",
    )

    parser.add_argument(
        "--sleep",
        default=0.25,
        type=float,
        help="Delay between HTTP requests.",
    )

    parser.add_argument(
        "--overwrite",
        action="store_true",
        help="Overwrite existing downloaded MARCXML files.",
    )

    parser.add_argument(
        "--max-files",
        default=0,
        type=int,
        help="Max local MARCXML files to parse. 0 = all.",
    )

    parser.add_argument(
        "--max-records",
        default=0,
        type=int,
        help="Max matching records to write. 0 = all.",
    )

    return parser.parse_args()


def main() -> int:
    args = parse_args()

    if args.download:
        queries = args.query or [
            "magazine",
            "periodical",
            "serial",
            "magazines",
            "periodicals",
        ]

        download_loc_records(
            raw_dir=args.raw_dir,
            queries=queries,
            max_pages=args.max_pages,
            page_count=args.page_count,
            sleep_seconds=args.sleep,
            overwrite=args.overwrite,
        )

        # If user did not explicitly override input_dir, parse the raw dir.
        if args.input_dir == DEFAULT_RAW_DIR:
            args.input_dir = args.raw_dir

    if not args.input_dir.exists():
        print(f"Input directory does not exist: {args.input_dir}", file=sys.stderr)
        return 1

    rows = build_rows(
        input_dir=args.input_dir,
        max_files=args.max_files,
        max_records=args.max_records,
    )

    count = write_csv(rows, args.output_csv)

    print("Done.")
    print(f"Rows written: {count}")
    print(f"CSV: {args.output_csv}")

    return 0


if __name__ == "__main__":
    raise SystemExit(main())