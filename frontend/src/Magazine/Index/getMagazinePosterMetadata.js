export function getMagazinePosterImage(images = []) {
  return images.find((image) => image.coverType === 'cover') || null;
}

export function getMagazinePosterBadge(title = '') {
  const words = title
    .split(/\s+/)
    .map((word) => word.trim())
    .filter(Boolean);

  if (!words.length) {
    return '?';
  }

  if (words.length === 1) {
    return words[0].slice(0, 2).toUpperCase();
  }

  return `${words[0][0]}${words[1][0]}`.toUpperCase();
}

export function getIssueCountLabel(issueCount = 0) {
  if (issueCount === 1) {
    return '1 issue';
  }

  return `${issueCount} issues`;
}

export function getFileCountLabel(fileCount = 0) {
  if (fileCount === 1) {
    return '1 file';
  }

  return `${fileCount} files`;
}
