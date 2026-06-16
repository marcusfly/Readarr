import PropTypes from 'prop-types';
import React, { Component } from 'react';
import { connect } from 'react-redux';
import { fetchMagazines } from 'Store/Actions/magazineActions';
import MagazineIndex from './MagazineIndex';

class MagazineIndexConnector extends Component {
  componentDidMount() {
    if (!this.props.isPopulated && !this.props.isFetching) {
      this.props.fetchMagazines();
    }
  }

  onRefreshPress = () => {
    this.props.fetchMagazines();
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
  fetchMagazines: PropTypes.func.isRequired
};

function mapStateToProps(state) {
  return {
    ...state.magazines
  };
}

export default connect(mapStateToProps, {
  fetchMagazines
})(MagazineIndexConnector);
