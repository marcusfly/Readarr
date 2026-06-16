import PropTypes from 'prop-types';
import React from 'react';
import styles from 'Author/NoAuthor.css';
import Button from 'Components/Link/Button';
import { kinds } from 'Helpers/Props';
import { buildAddNewSearchPath, searchScopes } from 'Search/searchScopes';
import translate from 'Utilities/String/translate';

function NoMagazine(props) {
  const {
    totalItems
  } = props;

  if (totalItems > 0) {
    return (
      <div>
        <div className={styles.message}>
          All magazines are hidden due to the applied filter.
        </div>
      </div>
    );
  }

  return (
    <div>
      <div className={styles.message}>
        No magazines found, to get started you'll want to add a new magazine or add an existing library location (Root Folder) and update.
      </div>

      <div className={styles.buttonContainer}>
        <Button
          to="/settings/mediamanagement"
          kind={kinds.PRIMARY}
        >
          {translate('AddRootFolder')}
        </Button>
      </div>

      <div className={styles.buttonContainer}>
        <Button
          to={buildAddNewSearchPath(searchScopes.MAGAZINES)}
          kind={kinds.PRIMARY}
        >
          Add New Magazine
        </Button>
      </div>
    </div>
  );
}

NoMagazine.propTypes = {
  totalItems: PropTypes.number.isRequired
};

export default NoMagazine;
