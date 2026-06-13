export default function generateUUIDv4(): string {
  return ([1e7, -1e3, -4e3, -8e3, -1e11] as const)
    .join('')
    .replace(/[018]/g, (c) => {
      const n = Number(c);
      const randomByte = crypto.getRandomValues(new Uint8Array(1))[0];
      // eslint-disable-next-line no-bitwise
      return (n ^ (randomByte & (15 >> (n / 4)))).toString(16);
    });
}
