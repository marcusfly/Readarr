# Metadata and Identity Contract

The metadata subsystem is fresh-install-only. Legacy Goodreads-derived database
ownership is not migrated.

## Providers

- `rreading-glasses` is the default provider and uses
  `https://hardcover.bookinfo.pro` unless overridden.
- `openlibrary` is the independent secondary provider.
- Provider APIs implement `IMetadataProviderV1` and advertise capabilities.
- Namespaced identifiers route refreshes back to the provider that created them.

## Identity

Entity identifiers use `provider:entity:value`, for example:

- `rreading-glasses:author:123`
- `rreading-glasses:work:456`
- `openlibrary:edition:OL7353617M`

Provider IDs remain authoritative within a provider. Normalized ISBN-13/ISBN-10 is
the cross-provider edition match key. If an ISBN is absent or malformed, matching
falls back to the namespaced edition ID.

Duplicate editions are accepted once per ISBN match key. A work monitors exactly
one edition, selected by rating popularity, metadata completeness, then stable
identifier order.

Redirects are followed only for edition, ISBN, and ASIN lookups. A provider 404 is
reported as a typed not-found result. Transient provider failures never delete or
rewrite local entities.

## Resilience

Successful GET responses are cached locally. Expired cache entries can be served
during transient HTTP, transport, or provider failures. ISBN and ASIN lookup try
the configured provider first, then another capable provider.

The change-feed timestamp is the refresh cursor. A limited, invalid, or unavailable
feed returns no cursor result and scheduled refresh falls back to age-based refresh.

Namespaced IDs and source links provide entity-level provenance.

## Acceptance Thresholds

The offline acceptance corpus must maintain:

- 50 authors, 200 works, and 400 editions for the rreading-glasses mapper.
- 100% unique namespaced author, work, and edition IDs.
- 100% normalized 13-character ISBN values in ISBN-bearing corpus editions.
- Exactly one monitored edition per populated work.
- Source provenance on every corpus author.
- Contract coverage for search, author/work hydration, ISBN redirect lookup,
  deletion, limited change feeds, provider outage, and independent-provider fallback.

Live endpoint smoke tests are supplemental because hosted-provider availability is
external state.
