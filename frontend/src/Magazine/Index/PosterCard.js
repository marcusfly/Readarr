import PropTypes from 'prop-types';
import React from 'react';
import Link from 'Components/Link/Link';
import PosterCover from './PosterCover';
import PosterInfo from './PosterInfo';
import styles from './PosterCard.css';

function PosterCard({
  magazine,
  to
}) {
  const statistics = magazine.statistics || {};
  const link = to || `/magazine/${magazine.id}`;

  return (
    <Link className={styles.link} to={link}>
      <div className={styles.posterCard}>
        <PosterCover
          title={magazine.title}
          images={magazine.images}
        />

        <PosterInfo
          title={magazine.title}
          publisher={magazine.publisher}
          monitored={magazine.monitored}
          issueCount={statistics.issueCount || 0}
          issueFileCount={statistics.issueFileCount || 0}
        />
      </div>
    </Link>
  );
}

PosterCard.propTypes = {
  magazine: PropTypes.shape({
    id: PropTypes.number.isRequired,
    title: PropTypes.string.isRequired,
    publisher: PropTypes.string,
    monitored: PropTypes.bool.isRequired,
    images: PropTypes.arrayOf(PropTypes.object),
    statistics: PropTypes.shape({
      issueCount: PropTypes.number,
      issueFileCount: PropTypes.number
    })
  }).isRequired,
  to: PropTypes.string
};

PosterCard.defaultProps = {
  to: ''
};

export default PosterCard;
