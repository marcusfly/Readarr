import PropTypes from 'prop-types';
import React from 'react';
import PosterCard from '../PosterCard';
import styles from './MagazineIndexPosters.css';

function MagazineIndexPosters({
  items,
  emptyMessage,
  getPosterLink
}) {
  if (!items.length) {
    return (
      <div className={styles.empty}>
        {emptyMessage}
      </div>
    );
  }

  return (
    <div className={styles.grid}>
      {
        items.map((magazine) => (
          <PosterCard
            key={magazine.id}
            magazine={magazine}
            to={getPosterLink(magazine)}
          />
        ))
      }
    </div>
  );
}

MagazineIndexPosters.propTypes = {
  items: PropTypes.arrayOf(PropTypes.shape({
    id: PropTypes.number.isRequired
  })).isRequired,
  emptyMessage: PropTypes.node,
  getPosterLink: PropTypes.func
};

MagazineIndexPosters.defaultProps = {
  emptyMessage: 'No magazines to display.',
  getPosterLink: (magazine) => `/magazine/${magazine.id}`
};

export default MagazineIndexPosters;
