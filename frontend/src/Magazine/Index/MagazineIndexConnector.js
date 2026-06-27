import PropTypes from 'prop-types';
import React, { Component } from 'react';
import { connect } from 'react-redux';
import { createSelector } from 'reselect';
import * as commandNames from 'Commands/commandNames';
import { executeCommand } from 'Store/Actions/commandActions';
import { fetchMagazines } from 'Store/Actions/magazineActions';
import createCommandExecutingSelector from 'Store/Selectors/createCommandExecutingSelector';
import MagazineIndex from './MagazineIndex';

function createMapStateToProps() {
  return createSelector(
    (state) => state.magazines,
    createCommandExecutingSelector(commandNames.RESCAN_MAGAZINE),
    (magazines, isRefreshingMagazines) => {
      return {
        ...magazines,
        isRefreshingMagazines
      };
    }
  );
}

class MagazineIndexConnector extends Component {
  componentDidMount() {
    if (!this.props.isPopulated && !this.props.isFetching) {
      this.props.fetchMagazines();
    }
  }

  onRefreshPress = () => {
    this.props.executeCommand({
      name: commandNames.RESCAN_MAGAZINE,
      addNewMagazines: true,
      addNewIssues: false,
      commandFinished: this.props.fetchMagazines
    });
  };

  render() {
    return (
      <MagazineIndex
        {...this.props}
        onRefreshPress={this.onRefreshPress}
      />
    );
  }
}

MagazineIndexConnector.propTypes = {
  isFetching: PropTypes.bool.isRequired,
  isPopulated: PropTypes.bool.isRequired,
  fetchMagazines: PropTypes.func.isRequired,
  executeCommand: PropTypes.func.isRequired
};

export default connect(createMapStateToProps, {
  fetchMagazines,
  executeCommand
})(MagazineIndexConnector);
