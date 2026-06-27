import PropTypes from 'prop-types';
import React, { Component } from 'react';
import { connect } from 'react-redux';
import { createSelector } from 'reselect';
import * as commandNames from 'Commands/commandNames';
import { executeCommand } from 'Store/Actions/commandActions';
import { fetchMagazines, saveMagazine } from 'Store/Actions/magazineActions';
import { fetchMagazineIssues, toggleMagazineIssueMonitored } from 'Store/Actions/magazineIssueActions';
import createCommandsSelector from 'Store/Selectors/createCommandsSelector';
import createDimensionsSelector from 'Store/Selectors/createDimensionsSelector';
import { findCommand, isCommandExecuting } from 'Utilities/Command';
import MagazineDetails from './MagazineDetails';

function createMapStateToProps() {
  return createSelector(
    (state, ownProps) => Number(ownProps.match.params.id),
    (state) => state.magazines,
    (state) => state.magazineIssues,
    createCommandsSelector(),
    createDimensionsSelector(),
    (magazineId, magazinesState, issuesState, commands, dimensions) => {
      const magazine = magazinesState.items.find((item) => item.id === magazineId);
      const issues = issuesState.items.filter((item) => item.magazineId === magazineId);
      const refreshCommand = findCommand(commands, { name: commandNames.RESCAN_MAGAZINE });
      const searchCommand = findCommand(commands, { name: commandNames.MAGAZINE_SEARCH });
      const isRefreshing = isCommandExecuting(refreshCommand) && refreshCommand.body.magazineId === magazineId;
      const isSearching = isCommandExecuting(searchCommand) && searchCommand.body.magazineId === magazineId;

      return {
        width: dimensions.width,
        magazinesIsFetching: magazinesState.isFetching,
        magazinesIsPopulated: magazinesState.isPopulated,
        issuesIsFetching: issuesState.isFetching,
        isRefreshing,
        isSearching,
        magazine: magazine ? {
          ...magazine,
          isSaving: magazinesState.isSaving
        } : null,
        issues
      };
    }
  );
}

function mapDispatchToProps(dispatch, ownProps) {
  const magazineId = Number(ownProps.match.params.id);

  return {
    fetchMagazines() {
      dispatch(fetchMagazines());
    },

    fetchMagazineIssues() {
      dispatch(fetchMagazineIssues({ magazineId }));
    },

    dispatchToggleMagazineIssueMonitored(issueId, monitored) {
      dispatch(toggleMagazineIssueMonitored({ issueId, monitored }));
    },

    dispatchSearchMagazine() {
      dispatch(executeCommand({
        name: commandNames.MAGAZINE_SEARCH,
        magazineId
      }));
    },

    dispatchRefreshMagazine() {
      dispatch(executeCommand({
        name: commandNames.RESCAN_MAGAZINE,
        magazineId
      }));
    },

    dispatchToggleMagazineMonitored(magazine) {
      dispatch(saveMagazine({
        id: magazine.id,
        monitored: !magazine.monitored
      }));
    }
  };
}

class MagazineDetailsConnector extends Component {
  componentDidMount() {
    this.populate();
  }

  componentDidUpdate(prevProps) {
    if (prevProps.match.params.id !== this.props.match.params.id) {
      this.populate();
    }

    if ((prevProps.isSearching && !this.props.isSearching) ||
        (prevProps.isRefreshing && !this.props.isRefreshing)) {
      this.props.fetchMagazines();
      this.props.fetchMagazineIssues();
    }
  }

  populate() {
    if (!this.props.magazinesIsPopulated && !this.props.magazinesIsFetching) {
      this.props.fetchMagazines();
    }

    this.props.fetchMagazineIssues();
  }

  onMonitorTogglePress = () => {
    if (this.props.magazine) {
      this.props.dispatchToggleMagazineMonitored(this.props.magazine);
    }
  };

  render() {
    return (
      <MagazineDetails
        width={this.props.width}
        isFetching={this.props.magazinesIsFetching || this.props.issuesIsFetching}
        isRefreshing={this.props.isRefreshing}
        isSearching={this.props.isSearching}
        magazine={this.props.magazine}
        issues={this.props.issues}
        onMonitorChange={this.props.dispatchToggleMagazineIssueMonitored}
        onMonitorTogglePress={this.onMonitorTogglePress}
        onRefresh={this.props.dispatchRefreshMagazine}
        onSearch={this.props.dispatchSearchMagazine}
      />
    );
  }
}

MagazineDetailsConnector.propTypes = {
  width: PropTypes.number.isRequired,
  match: PropTypes.shape({
    params: PropTypes.shape({
      id: PropTypes.string.isRequired
    }).isRequired
  }).isRequired,
  magazinesIsFetching: PropTypes.bool.isRequired,
  magazinesIsPopulated: PropTypes.bool.isRequired,
  issuesIsFetching: PropTypes.bool.isRequired,
  isRefreshing: PropTypes.bool.isRequired,
  isSearching: PropTypes.bool.isRequired,
  fetchMagazines: PropTypes.func.isRequired,
  fetchMagazineIssues: PropTypes.func.isRequired,
  dispatchToggleMagazineIssueMonitored: PropTypes.func.isRequired,
  dispatchToggleMagazineMonitored: PropTypes.func.isRequired,
  dispatchSearchMagazine: PropTypes.func.isRequired,
  dispatchRefreshMagazine: PropTypes.func.isRequired,
  magazine: PropTypes.object,
  issues: PropTypes.arrayOf(PropTypes.object).isRequired
};

MagazineDetailsConnector.defaultProps = {
  magazine: null
};

export default connect(createMapStateToProps, mapDispatchToProps)(MagazineDetailsConnector);
