import PropTypes from 'prop-types';
import React, { useMemo, useState } from 'react';
import { getMagazinePosterBadge, getMagazinePosterImage } from './getMagazinePosterMetadata';
import styles from './PosterCover.css';

function PosterCover({ title, images }) {
  const [hasError, setHasError] = useState(false);

  const cover = useMemo(() => {
    return getMagazinePosterImage(images);
  }, [images]);

  const badgeText = useMemo(() => {
    return getMagazinePosterBadge(title);
  }, [title]);

  if (cover && !hasError) {
    return (
      <div className={styles.cover}>
        <img
          className={styles.coverImage}
          src={cover.url}
          alt={`${title} cover`}
          onError={() => setHasError(true)}
        />
      </div>
    );
  }

  return (
    <div className={styles.cover}>
      <div className={styles.coverPlaceholder}>
        <span className={styles.coverText}>{badgeText}</span>
      </div>
    </div>
  );
}

PosterCover.propTypes = {
  title: PropTypes.string,
  images: PropTypes.arrayOf(PropTypes.object)
};

PosterCover.defaultProps = {
  title: '',
  images: []
};

export default PosterCover;
