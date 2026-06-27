import PropTypes from 'prop-types';
import React from 'react';
import { Link } from 'react-router-dom';
import PageContent from 'Components/Page/PageContent';
import PageContentBody from 'Components/Page/PageContentBody';
import MagazineIssueGraphic from './MagazineIssueGraphic';
import styles from './MagazineDetails.css';

function getIssueSortValue(issue) {
  return Date.UTC(
    issue.issueYear || 0,
    Math.max((issue.issueMonth || 1) - 1, 0),
    issue.issueDay || 1
  );
}

function formatIssue(issue) {
  const month = `${issue.issueMonth}`.padStart(2, '0');
  const day = issue.issueDay == null ? '' : `-${`${issue.issueDay}`.padStart(2, '0')}`;
  return `${issue.issueYear}-${month}${day}`;
}

function formatLongIssue(issue) {
  const date = new Date(getIssueSortValue(issue));

  return new Intl.DateTimeFormat('en-US', {
    year: 'numeric',
    month: 'long',
    day: issue.issueDay == null ? undefined : 'numeric'
  }).format(date);
}

function getCoverUrl(images = []) {
  return images.find((image) => image.coverType === 'cover')?.url || '';
}

function MagazineDetails({
  isFetching,
  magazine,
  issues = [],
  onMonitorChange
}) {
  if (isFetching && !magazine) {
    return (
      <PageContent title="Loading">
        <PageContentBody innerClassName={styles.pageContent}>
          <div>Loading magazine...</div>
        </PageContentBody>
      </PageContent>
    );
  }

  if (!magazine) {
    return (
      <PageContent title="Magazine Not Found">
        <PageContentBody innerClassName={styles.pageContent}>
          <p>Magazine not found.</p>
          <Link to="/magazine">Back to magazines</Link>
        </PageContentBody>
      </PageContent>
    );
  }

  const sortedIssues = [...issues].sort((left, right) => getIssueSortValue(right) - getIssueSortValue(left));
  const latestIssue = sortedIssues[0] || null;
  const latestImportedIssue = sortedIssues.find((issue) => issue.hasFile && getCoverUrl(issue.images)) || latestIssue;
  const heroImageUrl = latestImportedIssue ? getCoverUrl(latestImportedIssue.images) : '';
  const monitoredIssueCount = sortedIssues.filter((issue) => issue.monitored).length;
  const fileIssueCount = sortedIssues.filter((issue) => issue.hasFile).length;
  const latestIssueSummary = latestIssue ?
    `Latest issue: ${formatLongIssue(latestIssue)}` :
    'Latest issue: none yet';

  return (
    <PageContent title={magazine.title}>
      <PageContentBody innerClassName={styles.pageContent}>
        <p className={styles.breadcrumb}><Link to="/magazine">Magazines</Link></p>

        <section className={styles.overviewCard}>
          <div className={styles.heroColumn}>
            {
              heroImageUrl ?
                <img
                  className={styles.heroImage}
                  src={heroImageUrl}
                  alt={`${magazine.title} latest issue cover`}
                /> :
                <div className={styles.heroPlaceholder}>
                  <MagazineIssueGraphic
                    className={styles.heroPlaceholderGraphic}
                    images={[]}
                  />
                </div>
            }
          </div>

          <div className={styles.summaryColumn}>
            <h2 className={styles.title}>{magazine.title}</h2>
            <p className={styles.subtitle}>{magazine.publisher || 'Unknown publisher'}</p>

            <div className={styles.statRow}>
              <span className={styles.statPill}>{sortedIssues.length} issues tracked</span>
              <span className={styles.statPill}>{fileIssueCount} files imported</span>
              <span className={styles.statPill}>{monitoredIssueCount} monitored</span>
              <span className={styles.statPill}>{latestIssueSummary}</span>
            </div>

            <div className={styles.descriptionBlock}>
              <p className={styles.description}>
                {magazine.title} is currently tracked in your library with issue-level monitoring, imported file status, and generated first-page artwork from the newest available issue.
              </p>
            </div>

            <dl className={styles.detailGrid}>
              <div className={styles.detailItem}>
                <dt>Publisher</dt>
                <dd>{magazine.publisher || 'Unknown'}</dd>
              </div>

              <div className={styles.detailItem}>
                <dt>ISSN</dt>
                <dd>{magazine.issn || 'Not available'}</dd>
              </div>

              <div className={styles.detailItem}>
                <dt>Library Path</dt>
                <dd className={styles.pathValue}>{magazine.path || 'Not set'}</dd>
              </div>

              <div className={styles.detailItem}>
                <dt>Source</dt>
                <dd>
                  {
                    magazine.wikidataId ?
                      <a
                        className={styles.detailLink}
                        href={`https://www.wikidata.org/wiki/${magazine.wikidataId}`}
                        rel="noreferrer"
                        target="_blank"
                      >
                        Wikidata
                      </a> :
                      'Not linked'
                  }
                </dd>
              </div>
            </dl>
          </div>
        </section>

        <table className={styles.issuesTable}>
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
              sortedIssues.map((issue) => (
                <tr key={issue.id}>
                  <td className={styles.thumbnailColumn}>
                    <MagazineIssueGraphic images={issue.images} />
                  </td>
                  <td>{formatIssue(issue)}</td>
                  <td>{issue.releaseTitle || issue.issueNumber || '-'}</td>
                  <td>
                    <input
                      type="checkbox"
                      checked={issue.monitored}
                      onChange={(event) => onMonitorChange(issue.id, event.target.checked)}
                    />
                  </td>
                  <td>{issue.hasFile ? 'Yes' : 'No'}</td>
                  <td>{issue.quality?.quality?.name || issue.quality?.quality?.id || '-'}</td>
                </tr>
              ))
            }
          </tbody>
        </table>
      </PageContentBody>
    </PageContent>
  );
}

MagazineDetails.propTypes = {
  isFetching: PropTypes.bool,
  magazine: PropTypes.object,
  issues: PropTypes.arrayOf(PropTypes.object),
  onMonitorChange: PropTypes.func.isRequired
};

export default MagazineDetails;
