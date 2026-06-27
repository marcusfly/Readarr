function getMagazineIssueMonthSortValue(issueMonth) {
  if (issueMonth == null) {
    return 0;
  }

  return Math.max(issueMonth - 1, 0);
}

export function getMagazineIssueSortValue(issue) {
  return Date.UTC(
    issue.issueYear || 0,
    getMagazineIssueMonthSortValue(issue.issueMonth),
    issue.issueDay || 1
  );
}

export function formatMagazineIssueDate(issue) {
  if (issue.issueYear == null) {
    return 'Unknown';
  }

  if (issue.issueMonth == null) {
    return `${issue.issueYear}`;
  }

  const month = `${issue.issueMonth}`.padStart(2, '0');

  if (issue.issueDay == null) {
    return `${issue.issueYear}-${month}`;
  }

  const day = `${issue.issueDay}`.padStart(2, '0');

  return `${issue.issueYear}-${month}-${day}`;
}

export function getMagazineIssueTitle(issue) {
  return issue.releaseTitle || issue.title || issue.issueNumber || '-';
}

export function getMagazineIssueKey(issue) {
  if (issue.id != null) {
    return issue.id;
  }

  return [
    issue.issueYear || 'unknown',
    issue.issueMonth || 0,
    issue.issueDay || 0,
    getMagazineIssueTitle(issue)
  ].join(':');
}

export function createMagazineIssueYearGroups(issues = []) {
  const groupedByYear = issues.reduce((groups, issue) => {
    const year = issue.issueYear || 'Unknown';

    if (!groups[year]) {
      groups[year] = [];
    }

    groups[year].push(issue);

    return groups;
  }, {});

  return Object.keys(groupedByYear)
    .sort((left, right) => {
      if (left === 'Unknown') {
        return 1;
      }

      if (right === 'Unknown') {
        return -1;
      }

      return Number(right) - Number(left);
    })
    .map((year) => {
      const yearIssues = [...groupedByYear[year]].sort((left, right) => {
        return getMagazineIssueSortValue(right) - getMagazineIssueSortValue(left);
      });

      return {
        year,
        issueCount: yearIssues.length,
        monitoredCount: yearIssues.filter((issue) => issue.monitored).length,
        fileCount: yearIssues.filter((issue) => issue.hasFile).length,
        issues: yearIssues
      };
    });
}

export function getDefaultExpandedYearState(groups = [], defaultExpandedYears = 1) {
  return groups.reduce((state, group, index) => {
    state[group.year] = index < defaultExpandedYears;

    return state;
  }, {});
}

