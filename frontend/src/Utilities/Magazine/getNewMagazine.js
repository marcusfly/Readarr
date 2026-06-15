function getNewMagazine(magazine, payload) {
  const {
    monitor,
    rootFolderPath,
    qualityProfileId,
    metadataProfileId,
    tags,
    searchForMissingIssues = false
  } = payload;

  magazine.addOptions = {
    monitor,
    searchForMissingIssues
  };
  magazine.monitored = true;
  magazine.rootFolderPath = rootFolderPath;
  magazine.qualityProfileId = qualityProfileId;
  magazine.metadataProfileId = metadataProfileId;
  magazine.tags = tags;

  return magazine;
}

export default getNewMagazine;
