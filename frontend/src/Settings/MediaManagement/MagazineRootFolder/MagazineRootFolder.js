import PropTypes from 'prop-types';
import React, { Component } from 'react';
import Card from 'Components/Card';
import Label from 'Components/Label';
import ConfirmModal from 'Components/Modal/ConfirmModal';
import { kinds } from 'Helpers/Props';
import translate from 'Utilities/String/translate';
import EditMagazineRootFolderModal from './EditMagazineRootFolderModal';
import styles from '../RootFolder/RootFolder.css';

class MagazineRootFolder extends Component {
  constructor(props, context) {
    super(props, context);

    this.state = {
      isEditRootFolderModalOpen: false,
      isDeleteRootFolderModalOpen: false
    };
  }

  onEditRootFolderPress = () => {
    this.setState({ isEditRootFolderModalOpen: true });
  };

  onEditRootFolderModalClose = () => {
    this.setState({ isEditRootFolderModalOpen: false });
  };

  onDeleteRootFolderPress = () => {
    this.setState({
      isEditRootFolderModalOpen: false,
      isDeleteRootFolderModalOpen: true
    });
  };

  onDeleteRootFolderModalClose = () => {
    this.setState({ isDeleteRootFolderModalOpen: false });
  };

  onConfirmDeleteRootFolder = () => {
    this.props.onConfirmDeleteRootFolder(this.props.id);
  };

  render() {
    const {
      id,
      name,
      path
    } = this.props;

    return (
      <Card
        className={styles.rootFolder}
        overlayContent={true}
        onPress={this.onEditRootFolderPress}
      >
        <div className={styles.name}>
          {name}
        </div>

        <div className={styles.enabled}>
          <Label kind={kinds.SUCCESS}>
            {path}
          </Label>
        </div>

        <EditMagazineRootFolderModal
          id={id}
          isOpen={this.state.isEditRootFolderModalOpen}
          onModalClose={this.onEditRootFolderModalClose}
          onDeleteRootFolderPress={this.onDeleteRootFolderPress}
        />

        <ConfirmModal
          isOpen={this.state.isDeleteRootFolderModalOpen}
          kind={kinds.DANGER}
          title={translate('DeleteRootFolder')}
          message={translate('DeleteRootFolderMessageText', { name })}
          confirmLabel={translate('Delete')}
          onConfirm={this.onConfirmDeleteRootFolder}
          onCancel={this.onDeleteRootFolderModalClose}
        />
      </Card>
    );
  }
}

MagazineRootFolder.propTypes = {
  id: PropTypes.number.isRequired,
  name: PropTypes.string.isRequired,
  path: PropTypes.string.isRequired,
  onConfirmDeleteRootFolder: PropTypes.func.isRequired
};

export default MagazineRootFolder;
