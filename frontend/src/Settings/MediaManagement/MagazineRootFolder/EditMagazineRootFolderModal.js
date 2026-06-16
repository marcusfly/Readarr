import PropTypes from 'prop-types';
import React from 'react';
import Modal from 'Components/Modal/Modal';
import EditMagazineRootFolderModalContentConnector from './EditMagazineRootFolderModalContentConnector';

function EditMagazineRootFolderModal({ isOpen, onModalClose, ...otherProps }) {
  return (
    <Modal
      isOpen={isOpen}
      onModalClose={onModalClose}
    >
      <EditMagazineRootFolderModalContentConnector
        {...otherProps}
        onModalClose={onModalClose}
      />
    </Modal>
  );
}

EditMagazineRootFolderModal.propTypes = {
  isOpen: PropTypes.bool.isRequired,
  onModalClose: PropTypes.func.isRequired
};

export default EditMagazineRootFolderModal;
