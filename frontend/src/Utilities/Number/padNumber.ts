function padNumber(
  input: number | string | null | undefined,
  width: number,
  paddingCharacter: string | number = 0
): string {
  if (input == null) {
    return '';
  }

  const str = `${input}`;
  return str.length >= width
    ? str
    : new Array(width - str.length + 1).join(String(paddingCharacter)) + str;
}

export default padNumber;
