import PropTypes from 'prop-types';
import React, { Component } from 'react';
import { connect } from 'react-redux';
import { fetchMagazines } from 'Store/Actions/magazineActions';
import { fetchMagazineIssues, toggleMagazineIssueMonitored } from 'Store/Actions/magazineIssueActions';
import MagazineDetails from './MagazineDetails';

class MagazineDetailsConnector extends Component {
  componentDidMount() {
    this.populate();
  }

  componentDidUpdate(prevProps) {
    if (prevProps.match.params.id !== this.props.match.params.id) {
      this.populate();
    }
  }

  populate() {
    const magazineId = Number(this.props.match.params.id);

    if (!this.props.magazinesIsPopulated && !this.props.magazinesIsFetching) {
      this.props.fetchMagazines();
    }

    this.props.fetchMagazineIssues({ magazineId });
  }

  render() {
    return (
      <MagazineDetails
        isFetching={this.props.magazinesIsFetching || this.props.issuesIsFetching}
        magazine={this.props.magazine}
        issues={this.props.issues}
        onMonitorChange={this.props.dispatchToggleMagazineIssueMonitored}
      />
    );
  }
}

MagazineDetailsConnector.propTypes = {
  match: PropTypes.shape({
    params: PropTypes.shape({
      id: PropTypes.string.isRequired
    }).isRequired
  }).isRequired,
  magazinesIsFetching: PropTypes.bool.isRequired,
  magazinesIsPopulated: PropTypes.bool.isRequired,
  issuesIsFetching: PropTypes.bool.isRequired,
  fetchMagazines: PropTypes.func.isRequired,
  fetchMagazineIssues: PropTypes.func.isRequired,
  dispatchToggleMagazineIssueMonitored: PropTypes.func.isRequired,
  magazine: PropTypes.object,
  issues: PropTypes.arrayOf(PropTypes.object).isRequired
};

function mapStateToProps(state, ownProps) {
  const magazineId = Number(ownProps.match.params.id);
  const magazine = state.magazines.items.find((item) => item.id === magazineId);
  const issues = state.magazineIssues.items.filter((item) => item.magazineId === magazineId);

  return {
    magazinesIsFetching: state.magazines.isFetching,
    magazinesIsPopulated: state.magazines.isPopulated,
    issuesIsFetching: state.magazineIssues.isFetching,
    magazine,
    issues
  };
}

function mapDispatchToProps(dispatch) {
  return {
    fetchMagazines() {
      dispatch(fetchMagazines());
    },

    fetchMagazineIssues(payload) {
      dispatch(fetchMagazineIssues(payload));
    },

    dispatchToggleMagazineIssueMonitored(issueId, monitored) {
      dispatch(toggleMagazineIssueMonitored({ issueId, monitored }));
    }
  };
}

export default connect(mapStateToProps, mapDispatchToProps)(MagazineDetailsConnector);
