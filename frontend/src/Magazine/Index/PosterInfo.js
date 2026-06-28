import PropTypes from 'prop-types';
import React from 'react';
import Icon from 'Components/Icon';
import { icons } from 'Helpers/Props';
import {
  getFileCountLabel,
  getIssueCountLabel
} from './getMagazinePosterMetadata';
import styles from './PosterInfo.css';

function PosterInfo({
  title,
  publisher,
  monitored,
  issueCount,
  issueFileCount
}) {
  const monitoredClassName = monitored ? styles.monitored : styles.unmonitored;

  return (
    <div className={styles.info}>
      <div className={styles.titleRow}>
        <Icon
          className={`${styles.monitoredIcon} ${monitoredClassName}`}
          name={monitored ? icons.MONITORED : icons.UNMONITORED}
          title={monitored ? 'Monitored' : 'Unmonitored'}
        />

        <div className={styles.title} title={title}>
          {title}
        </div>
      </div>

      <div className={styles.publisher} title={publisher || 'Unknown publisher'}>
        {publisher || 'Unknown publisher'}
      </div>

      <div className={styles.stats}>
        <div className={styles.stat}>
          <span className={styles.statLabel}>Issues</span>
          <span className={styles.statValue}>{getIssueCountLabel(issueCount)}</span>
        </div>

        <div className={styles.stat}>
          <span className={styles.statLabel}>Files</span>
          <span className={styles.statValue}>{getFileCountLabel(issueFileCount)}</span>
        </div>
      </div>
    </div>
  );
}

PosterInfo.propTypes = {
  title: PropTypes.string.isRequired,
  publisher: PropTypes.string,
  monitored: PropTypes.bool.isRequired,
  issueCount: PropTypes.number.isRequired,
  issueFileCount: PropTypes.number.isRequired
};

PosterInfo.defaultProps = {
  publisher: ''
};

export default PosterInfo;
