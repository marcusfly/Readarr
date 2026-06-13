export default function getPathWithUrlBase(path: string): string {
  return `${window.Readarr.urlBase}${path}`;
}
