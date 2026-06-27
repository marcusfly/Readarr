import PropTypes from 'prop-types';
import React, { useMemo, useState } from 'react';
import styles from './MagazineIndex.css';

function getBadgeText(title) {
  const words = (title || '')
    .split(/\s+/)
    .map((word) => word.trim())
    .filter(Boolean);

  if (words.length === 0) {
    return '?';
  }

  if (words.length === 1) {
    return words[0].slice(0, 2).toUpperCase();
  }

  return `${words[0][0]}${words[1][0]}`.toUpperCase();
}

function MagazineGraphic({ title, images }) {
  const imageUrl = useMemo(() => {
    return images.find((image) => image.coverType === 'cover')?.url;
  }, [images]);

  const [hasError, setHasError] = useState(false);
  const shouldRenderImage = imageUrl && !hasError;

  return (
    <div
      className={styles.graphic}
      aria-hidden="true"
    >
      {
        shouldRenderImage ?
          <img
            className={styles.graphicImage}
            src={imageUrl}
            alt=""
            onError={() => setHasError(true)}
          /> :
          <>
            <div className={styles.graphicPlaceholder} />
            <span className={styles.graphicText}>
              {getBadgeText(title)}
            </span>
          </>
      }
    </div>
  );
}

MagazineGraphic.propTypes = {
  title: PropTypes.string,
  images: PropTypes.arrayOf(PropTypes.object)
};

MagazineGraphic.defaultProps = {
  title: '',
  images: []
};

export default MagazineGraphic;
