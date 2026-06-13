const pages = {
  FIRST: 'first',
  PREVIOUS: 'previous',
  NEXT: 'next',
  LAST: 'last',
  EXACT: 'exact',
} as const;

export type Page = (typeof pages)[keyof typeof pages];

export default pages;
