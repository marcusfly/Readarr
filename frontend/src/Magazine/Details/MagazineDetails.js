import PropTypes from 'prop-types';
import React from 'react';
import { Link } from 'react-router-dom';

function formatIssue(issue) {
  const month = `${issue.issueMonth}`.padStart(2, '0');
  const day = issue.issueDay == null ? '' : `-${`${issue.issueDay}`.padStart(2, '0')}`;
  return `${issue.issueYear}-${month}${day}`;
}

function MagazineDetails({
  isFetching,
  magazine,
  issues = [],
  onMonitorChange
}) {
  if (isFetching && !magazine) {
    return <div>Loading magazine...</div>;
  }

  if (!magazine) {
    return (
      <div className="page-content">
        <p>Magazine not found.</p>
        <Link to="/magazine">Back to magazines</Link>
      </div>
    );
  }

  return (
    <div className="page-content">
      <p><Link to="/magazine">Magazines</Link></p>
      <h2>{magazine.title}</h2>
      <p>{magazine.publisher || 'Unknown publisher'}</p>
      <p>{magazine.path}</p>

      <table>
        <thead>
          <tr>
            <th>Issue</th>
            <th>Title</th>
            <th>Monitored</th>
            <th>Has File</th>
            <th>Quality</th>
          </tr>
        </thead>
        <tbody>
          {
            issues.map((issue) => (
              <tr key={issue.id}>
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
    </div>
  );
}

MagazineDetails.propTypes = {
  isFetching: PropTypes.bool,
  magazine: PropTypes.object,
  issues: PropTypes.arrayOf(PropTypes.object),
  onMonitorChange: PropTypes.func.isRequired
};

export default MagazineDetails;
