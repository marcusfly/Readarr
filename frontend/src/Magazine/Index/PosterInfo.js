import PropTypes from 'prop-types';
import React from 'react';
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
      <div className={styles.title} title={title}>
        {title}
      </div>

      <div className={`${styles.status} ${monitoredClassName}`}>
        {monitored ? 'Monitored' : 'Unmonitored'}
      </div>

      <div className={styles.publisher} title={publisher || 'Unknown publisher'}>
        {publisher || 'Unknown publisher'}
      </div>

      <div className={styles.meta}>
        {getIssueCountLabel(issueCount)}
      </div>

      <div className={styles.meta}>
        {getFileCountLabel(issueFileCount)}
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
