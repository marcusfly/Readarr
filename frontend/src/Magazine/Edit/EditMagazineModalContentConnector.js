import _ from 'lodash';
import PropTypes from 'prop-types';
import React, { Component } from 'react';
import { connect } from 'react-redux';
import { createSelector } from 'reselect';
import { saveMagazine, setMagazineValue } from 'Store/Actions/magazineActions';
import selectSettings from 'Store/Selectors/selectSettings';
import monitorOptions from 'Utilities/Magazine/monitorOptions';
import EditMagazineModalContent from './EditMagazineModalContent';

function createMapStateToProps() {
  return createSelector(
    (state) => state.magazines,
    (state, { magazineId }) => magazineId,
    (magazineState, magazineId) => {
      const magazine = magazineState.items.find((item) => item.id === magazineId);

      if (!magazine) {
        return {};
      }

      const {
        isSaving,
        saveError,
        pendingChanges
      } = magazineState;

      const settings = selectSettings(_.pick(magazine, [
        'monitored',
        'qualityProfileId',
        'metadataProfileId',
        'tags'
      ]), pendingChanges, saveError);

      return {
        title: magazine.title,
        publisher: magazine.publisher,
        issn: magazine.issn,
        path: magazine.path,
        isSaving,
        saveError,
        monitor: {
          value: magazine.addOptions?.monitor || monitorOptions[0].key,
          errors: [],
          warnings: []
        },
        searchForMissingIssues: {
          value: magazine.addOptions?.searchForMissingIssues || false,
          errors: [],
          warnings: []
        },
        item: settings.settings,
        ...settings
      };
    }
  );
}

const mapDispatchToProps = {
  dispatchSaveMagazine: saveMagazine,
  dispatchSetMagazineValue: setMagazineValue
};

class EditMagazineModalContentConnector extends Component {
  componentDidUpdate(prevProps) {
    if (prevProps.isSaving && !this.props.isSaving && !this.props.saveError) {
      this.props.onModalClose();
    }
  }

  onInputChange = ({ name, value }) => {
    if (name === 'monitor' || name === 'searchForMissingIssues') {
      const addOptions = {
        ...(this.props.monitor ? { monitor: this.props.monitor.value } : {}),
        ...(this.props.searchForMissingIssues ? { searchForMissingIssues: this.props.searchForMissingIssues.value } : {})
      };

      addOptions[name] = value;

      this.props.dispatchSetMagazineValue({
        name: 'addOptions',
        value: addOptions
      });

      return;
    }

    this.props.dispatchSetMagazineValue({ name, value });
  };

  onSavePress = () => {
    this.props.dispatchSaveMagazine({
      id: this.props.magazineId
    });
  };

  render() {
    return (
      <EditMagazineModalContent
        {...this.props}
        onInputChange={this.onInputChange}
        onSavePress={this.onSavePress}
      />
    );
  }
}

EditMagazineModalContentConnector.propTypes = {
  magazineId: PropTypes.number.isRequired,
  isSaving: PropTypes.bool,
  saveError: PropTypes.object,
  monitor: PropTypes.object,
  searchForMissingIssues: PropTypes.object,
  dispatchSaveMagazine: PropTypes.func.isRequired,
  dispatchSetMagazineValue: PropTypes.func.isRequired,
  onModalClose: PropTypes.func.isRequired
};

EditMagazineModalContentConnector.defaultProps = {
  isSaving: false,
  saveError: null,
  monitor: null,
  searchForMissingIssues: null
};

export default connect(createMapStateToProps, mapDispatchToProps)(EditMagazineModalContentConnector);
