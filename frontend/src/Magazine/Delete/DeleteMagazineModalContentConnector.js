import { push } from 'connected-react-router';
import PropTypes from 'prop-types';
import React, { Component } from 'react';
import { connect } from 'react-redux';
import { createSelector } from 'reselect';
import { deleteMagazine } from 'Store/Actions/magazineActions';
import DeleteMagazineModalContent from './DeleteMagazineModalContent';

function createMapStateToProps() {
  return createSelector(
    (state) => state.magazines.items,
    (state, { magazineId }) => magazineId,
    (magazines, magazineId) => {
      return magazines.find((magazine) => magazine.id === magazineId) || {};
    }
  );
}

const mapDispatchToProps = {
  push,
  deleteMagazine
};

class DeleteMagazineModalContentConnector extends Component {
  onDeletePress = (deleteFiles) => {
    this.props.deleteMagazine({
      id: this.props.magazineId,
      deleteFiles
    });

    this.props.onModalClose(true);
    this.props.push(`${window.Readarr.urlBase}/magazine`);
  };

  render() {
    return (
      <DeleteMagazineModalContent
        {...this.props}
        onDeletePress={this.onDeletePress}
      />
    );
  }
}

DeleteMagazineModalContentConnector.propTypes = {
  magazineId: PropTypes.number.isRequired,
  push: PropTypes.func.isRequired,
  onModalClose: PropTypes.func.isRequired,
  deleteMagazine: PropTypes.func.isRequired
};

export default connect(createMapStateToProps, mapDispatchToProps)(DeleteMagazineModalContentConnector);
