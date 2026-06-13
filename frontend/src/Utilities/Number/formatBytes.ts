import { filesize } from 'filesize';

function formatBytes(input: number | string, showBits = false): string {
  const size = Number(input);

  if (isNaN(size)) {
    return '';
  }

  return `${filesize(size, {
    base: 2,
    round: 1,
    bits: showBits,
  })}`;
}

export default formatBytes;
