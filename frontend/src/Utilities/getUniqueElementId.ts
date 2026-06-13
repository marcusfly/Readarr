let i = 0;

// Returns a HTML 4.0 compliant element ID (http://stackoverflow.com/a/79022)
export default function getUniqueElementId(): string {
  return `id-${i++}`;
}
