export const searchScopes = {
  ALL: 'All',
  BOOKS: 'Books',
  MAGAZINES: 'Magazines',
  COMICS: 'Comics'
};

export const searchScopeOptions = [
  { key: searchScopes.ALL, value: 'All' },
  { key: searchScopes.BOOKS, value: 'Books' },
  { key: searchScopes.MAGAZINES, value: 'Magazines' },
  { key: searchScopes.COMICS, value: 'Comics' }
];

export function normalizeSearchScope(scope) {
  switch ((scope || '').toLowerCase()) {
    case 'books':
      return searchScopes.BOOKS;
    case 'magazines':
      return searchScopes.MAGAZINES;
    case 'comics':
      return searchScopes.COMICS;
    default:
      return searchScopes.ALL;
  }
}

export function getDefaultSearchScopeFromPath(pathname) {
  if (!pathname) {
    return searchScopes.ALL;
  }

  if (pathname.startsWith('/magazine')) {
    return searchScopes.MAGAZINES;
  }

  if (pathname.startsWith('/comic')) {
    return searchScopes.COMICS;
  }

  if (
    pathname.startsWith('/books') ||
    pathname.startsWith('/book') ||
    pathname.startsWith('/authors') ||
    pathname.startsWith('/author') ||
    pathname === '/'
  ) {
    return searchScopes.BOOKS;
  }

  return searchScopes.ALL;
}

export function buildAddNewSearchPath(scope, term) {
  const params = new URLSearchParams();
  const normalizedScope = normalizeSearchScope(scope);

  if (normalizedScope !== searchScopes.ALL) {
    params.set('scope', normalizedScope);
  }

  if (term) {
    params.set('term', term);
  }

  const query = params.toString();

  return query ? `/add/search?${query}` : '/add/search';
}
