import PropTypes from 'prop-types';
import React from 'react';
import BookQuality from 'Book/BookQuality';
import MonitorToggleButton from 'Components/MonitorToggleButton';
import {
  formatMagazineIssueDate,
  getMagazineIssueKey,
  getMagazineIssueTitle
} from './createMagazineIssueYearGroups';
import MagazineIssueGraphic from './MagazineIssueGraphic';
import styles from './MagazineDetails.css';

function MagazineIssueRow({ issue, onMonitorChange }) {
  const issueId = issue.id;
  const isMonitorDisabled = issueId == null || !onMonitorChange;
  const issueTitle = getMagazineIssueTitle(issue);

  const handleMonitorChange = (monitored, options = {}) => {
    onMonitorChange(issueId, monitored, issue, options.event);
  };

  return (
    <tr key={getMagazineIssueKey(issue)} className={styles.yearIssueRow}>
      <td className={`${styles.yearIssueCell} ${styles.yearIssueCoverCell}`}>
        <MagazineIssueGraphic images={issue.images} />
      </td>

      <td className={`${styles.yearIssueCell} ${styles.yearIssuePrimaryCell}`}>
        <div className={styles.yearIssueDate}>{formatMagazineIssueDate(issue)}</div>
        <div className={styles.yearIssueSubtext}>
          {issue.issueNumber ? `Issue ${issue.issueNumber}` : 'Tracked issue'}
        </div>
      </td>

      <td className={`${styles.yearIssueCell} ${styles.yearIssueTitleCell}`}>
        <div className={styles.yearIssueTitle}>{issueTitle}</div>
        <div className={`${styles.yearIssueSubtext} ${styles.yearIssueTitleMuted}`}>
          {issue.releaseTitle ? 'Matched release title' : 'No release title available'}
        </div>
      </td>

      <td className={`${styles.yearIssueCell} ${styles.yearIssueMonitorCell}`}>
        <MonitorToggleButton
          monitored={!!issue.monitored}
          isDisabled={isMonitorDisabled}
          isSaving={false}
          onPress={handleMonitorChange}
        />
      </td>

      <td className={`${styles.yearIssueCell} ${styles.yearIssueStatusCell}`}>
        <span
          className={`${styles.yearIssueBadge} ${issue.hasFile ? styles.yearIssueBadgeAvailable : styles.yearIssueBadgeMissing}`}
        >
          {issue.hasFile ? 'Has File' : 'Missing'}
        </span>
      </td>

      <td className={styles.yearIssueCell}>
        {
          issue.quality ?
            <span className={`${styles.yearIssueBadge} ${styles.yearIssueBadgeQuality}`}>
              <BookQuality quality={issue.quality} showRevision={true} />
            </span> :
            <span className={styles.yearIssueBadge}>Unknown</span>
        }
      </td>
    </tr>
  );
}

MagazineIssueRow.propTypes = {
  issue: PropTypes.shape({
    hasFile: PropTypes.bool,
    id: PropTypes.number,
    images: PropTypes.arrayOf(PropTypes.object),
    issueDay: PropTypes.number,
    issueMonth: PropTypes.number,
    issueNumber: PropTypes.oneOfType([PropTypes.string, PropTypes.number]),
    issueYear: PropTypes.number,
    monitored: PropTypes.bool,
    quality: PropTypes.object,
    releaseTitle: PropTypes.string,
    title: PropTypes.string
  }).isRequired,
  onMonitorChange: PropTypes.func
};

MagazineIssueRow.defaultProps = {
  onMonitorChange: null
};

export default MagazineIssueRow;
