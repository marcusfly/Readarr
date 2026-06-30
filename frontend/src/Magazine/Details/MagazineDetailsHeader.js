import PropTypes from 'prop-types';
import React from 'react';
import Icon from 'Components/Icon';
import Label from 'Components/Label';
import MonitorToggleButton from 'Components/MonitorToggleButton';
import { icons, sizes } from 'Helpers/Props';
import MagazineIssueGraphic from './MagazineIssueGraphic';
import styles from './MagazineDetailsHeader.css';

function formatLatestIssue(latestIssue) {
  if (!latestIssue) {
    return 'No issue yet';
  }

  const date = new Date(Date.UTC(
    latestIssue.issueYear || 0,
    Math.max((latestIssue.issueMonth || 1) - 1, 0),
    latestIssue.issueDay || 1
  ));

  return new Intl.DateTimeFormat('en-US', {
    year: 'numeric',
    month: 'long',
    day: latestIssue.issueDay == null ? undefined : 'numeric'
  }).format(date);
}

function getCoverImage(images = []) {
  return images.find((image) => image.coverType === 'cover');
}

function getBackdropImage(images = []) {
  return images.find((image) => image.coverType === 'fanart') || getCoverImage(images);
}

function MagazineDetailsHeader({
  width,
  magazine,
  latestIssue,
  monitoredIssueCount,
  fileIssueCount,
  onMonitorTogglePress
}) {
  const cover = getCoverImage(magazine.images);
  const backdrop = getBackdropImage(magazine.images);
  const isMonitored = magazine.monitored;

  return (
    <div className={styles.header} style={{ width }}>
      <div
        className={styles.backdrop}
        style={
          backdrop ?
            { backgroundImage: `url(${backdrop.url})` } :
            null
        }
      >
        <div className={styles.backdropOverlay} />
      </div>

      <div className={styles.headerContent}>
        <div className={styles.coverContainer}>
          {
            cover ?
              <img
                className={styles.cover}
                src={cover.url}
                alt={`${magazine.title} cover`}
              /> :
              <div className={styles.coverPlaceholder}>
                <MagazineIssueGraphic images={[]} />
              </div>
          }
        </div>

        <div className={styles.info}>
          <div className={styles.titleRow}>
            <div className={styles.titleContainer}>
              <div className={styles.toggleMonitoredContainer}>
                <MonitorToggleButton
                  className={styles.monitorToggleButton}
                  monitored={isMonitored}
                  isSaving={magazine.isSaving}
                  size={40}
                  onPress={onMonitorTogglePress}
                />
              </div>

              <div className={styles.titleBlock}>
                <div className={styles.title}>{magazine.title}</div>
                <div className={styles.subtitle}>{magazine.publisher || 'Unknown publisher'}</div>
              </div>
            </div>
          </div>

          <div className={styles.detailsLabels}>
            <Label
              className={styles.detailsLabel}
              size={sizes.LARGE}
            >
              <Icon
                name={icons.MONITORED}
                size={17}
              />

              <span className={styles.labelText}>
                {isMonitored ? 'Monitored' : 'Unmonitored'}
              </span>
            </Label>

            <Label
              className={styles.detailsLabel}
              size={sizes.LARGE}
            >
              <Icon
                name={icons.INTERACTIVE}
                size={17}
              />

              <span className={styles.labelText}>
                {monitoredIssueCount} monitored issues
              </span>
            </Label>

            <Label
              className={styles.detailsLabel}
              size={sizes.LARGE}
            >
              <Icon
                name={icons.ORGANIZE}
                size={17}
              />

              <span className={styles.labelText}>
                {fileIssueCount} imported files
              </span>
            </Label>

            <Label
              className={styles.detailsLabel}
              size={sizes.LARGE}
            >
              <Icon
                name={icons.CALENDAR}
                size={17}
              />

              <span className={styles.labelText}>
                {formatLatestIssue(latestIssue)}
              </span>
            </Label>

            {
              magazine.issn &&
                <Label
                  className={styles.detailsLabel}
                  size={sizes.LARGE}
                >
                  <Icon
                    name={icons.INFO}
                    size={17}
                  />

                  <span className={styles.labelText}>
                    ISSN {magazine.issn}
                  </span>
                </Label>
            }

            {
              magazine.wikidataId &&
                <a
                  className={styles.externalLink}
                  href={`https://www.wikidata.org/wiki/${magazine.wikidataId}`}
                  rel="noreferrer"
                  target="_blank"
                >
                  <Label
                    className={styles.detailsLabel}
                    size={sizes.LARGE}
                  >
                    <Icon
                      name={icons.EXTERNAL_LINK}
                      size={17}
                    />

                    <span className={styles.labelText}>
                      Links
                    </span>
                  </Label>
                </a>
            }
          </div>

          <div className={styles.overview}>
            {magazine.language ? `${magazine.language} magazine` : 'Magazine'} tracked with title-level actions and issue-level monitoring for download and import workflows.
          </div>
        </div>
      </div>
    </div>
  );
}

MagazineDetailsHeader.propTypes = {
  width: PropTypes.number.isRequired,
  magazine: PropTypes.object.isRequired,
  latestIssue: PropTypes.object,
  monitoredIssueCount: PropTypes.number.isRequired,
  fileIssueCount: PropTypes.number.isRequired,
  onMonitorTogglePress: PropTypes.func.isRequired
};

MagazineDetailsHeader.defaultProps = {
  latestIssue: null
};

export default MagazineDetailsHeader;
