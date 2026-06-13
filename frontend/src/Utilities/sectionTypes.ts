const sectionTypes = {
  COLLECTION: 'collection',
  MODEL: 'model',
} as const;

export type SectionType = (typeof sectionTypes)[keyof typeof sectionTypes];

export default sectionTypes;
