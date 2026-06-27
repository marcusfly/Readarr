import PropTypes from 'prop-types';
import React, { useMemo } from 'react';
import styles from './MagazineDetails.css';

function MagazineIssueGraphic({ className, images }) {
  const coverUrl = useMemo(() => {
    return images.find((image) => image.coverType === 'cover')?.url;
  }, [images]);

  const graphicClassName = [styles.issueGraphic, className].filter(Boolean).join(' ');

  if (coverUrl) {
    return (
      <span className={graphicClassName}>
        <img
          className={styles.issueGraphicImage}
          src={coverUrl}
          alt=""
        />
      </span>
    );
  }

  return (
    <span className={graphicClassName}>
      <span className={styles.issueGraphicPlaceholder}>-</span>
    </span>
  );
}

MagazineIssueGraphic.propTypes = {
  className: PropTypes.string,
  images: PropTypes.arrayOf(PropTypes.object)
};

MagazineIssueGraphic.defaultProps = {
  className: '',
  images: []
};

export default MagazineIssueGraphic;
