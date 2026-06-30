import classNames from 'classnames';
import PropTypes from 'prop-types';
import React from 'react';
import Icon from 'Components/Icon';
import { icons } from 'Helpers/Props';
import {
  formatMagazineIssueDate,
  getMagazineIssueTitle
} from './createMagazineIssueYearGroups';
import MagazineIssueRow from './MagazineIssueRow';
import styles from './MagazineDetails.css';

function getYearGroupSummary(group) {
  return {
    issueCount: `${group.issueCount} issues`,
    monitoredCount: `${group.monitoredCount || 0} monitored`,
    fileCount: `${group.fileCount || 0} files`
  };
}

function MagazineIssueYearGroup({
  group,
  isExpanded,
  onExpandPress,
  onMonitorChange
}) {
  const summary = getYearGroupSummary(group);
  const latestIssue = group.issues[0];

  const handleExpandPress = () => {
    onExpandPress(group.year, !isExpanded, group);
  };

  return (
    <section
      className={classNames(
        styles.yearGroup,
        isExpanded && styles.yearGroupExpanded
      )}
    >
      <div className={styles.yearGroupHeader}>
        <div className={styles.yearGroupLeft}>
          <div className={styles.yearGroupIdentity}>
            <div className={styles.yearGroupTitle}>{group.year}</div>
            <div className={styles.yearGroupCount}>
              {group.fileCount || 0} / {group.issueCount}
            </div>
            <div className={styles.yearGroupMeta}>
              <span className={styles.yearGroupMetaItem}>{summary.issueCount}</span>
              <span className={styles.yearGroupMetaItem}>{summary.monitoredCount}</span>
              <span className={styles.yearGroupMetaItem}>{summary.fileCount}</span>
              <span className={styles.yearGroupMetaItem}>
                <Icon
                  name={icons.CALENDAR}
                  size={12}
                  title="Latest issue"
                />
                Latest {latestIssue ? formatMagazineIssueDate(latestIssue) : 'Unknown'}
              </span>
            </div>
          </div>
        </div>

        <button
          type="button"
          aria-expanded={isExpanded}
          onClick={handleExpandPress}
          className={styles.expandButton}
        >
          <Icon
            className={styles.expandButtonIcon}
            name={isExpanded ? icons.COLLAPSE : icons.EXPAND}
            size={24}
            title={isExpanded ? 'Collapse year group' : 'Expand year group'}
          />
        </button>

        <div className={styles.yearGroupStatus}>
          <span className={`${styles.yearGroupStatusPill} ${styles.yearGroupStatusPillMonitored}`}>
            {summary.monitoredCount}
          </span>
          <span className={`${styles.yearGroupStatusPill} ${styles.yearGroupStatusPillMissing}`}>
            {group.issueCount - (group.fileCount || 0)} missing
          </span>
          <span className={`${styles.yearGroupStatusPill} ${styles.yearGroupStatusPillFiles}`}>
            {summary.fileCount}
          </span>
        </div>
      </div>

      {
        isExpanded &&
          <div className={styles.yearGroupBody}>
            <div className={styles.yearGroupTableWrap}>
              <table className={styles.yearGroupTable}>
                <thead>
                  <tr>
                    <th className={styles.thumbnailColumn}>Cover</th>
                    <th>Issue</th>
                    <th>Title</th>
                    <th>Monitored</th>
                    <th>Has File</th>
                    <th>Quality</th>
                  </tr>
                </thead>
                <tbody>
                  {
                    group.issues.map((issue) => (
                      <MagazineIssueRow
                        key={issue.id || `${group.year}-${issue.issueMonth || 0}-${issue.issueDay || 0}-${getMagazineIssueTitle(issue)}`}
                        issue={issue}
                        onMonitorChange={onMonitorChange}
                      />
                    ))
                  }
                </tbody>
              </table>
            </div>

            <div className={styles.collapseButtonContainer}>
              <button
                type="button"
                aria-expanded={isExpanded}
                onClick={handleExpandPress}
                className={styles.expandButton}
              >
                <Icon
                  className={styles.collapseButtonIcon}
                  name={icons.COLLAPSE}
                  size={24}
                  title="Collapse year group"
                />
              </button>
            </div>
          </div>
      }
    </section>
  );
}

MagazineIssueYearGroup.propTypes = {
  group: PropTypes.shape({
    fileCount: PropTypes.number,
    issueCount: PropTypes.number.isRequired,
    issues: PropTypes.arrayOf(PropTypes.object).isRequired,
    monitoredCount: PropTypes.number,
    year: PropTypes.oneOfType([PropTypes.number, PropTypes.string]).isRequired
  }).isRequired,
  isExpanded: PropTypes.bool,
  onExpandPress: PropTypes.func,
  onMonitorChange: PropTypes.func
};

MagazineIssueYearGroup.defaultProps = {
  isExpanded: false,
  onExpandPress: () => {},
  onMonitorChange: null
};

export default MagazineIssueYearGroup;
