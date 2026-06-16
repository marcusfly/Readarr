import PropTypes from 'prop-types';
import React, { Component } from 'react';
import Card from 'Components/Card';
import FieldSet from 'Components/FieldSet';
import Icon from 'Components/Icon';
import PageSectionContent from 'Components/Page/PageSectionContent';
import { icons } from 'Helpers/Props';
import sortByName from 'Utilities/Array/sortByName';
import EditMagazineRootFolderModal from './EditMagazineRootFolderModal';
import MagazineRootFolder from './MagazineRootFolder';
import styles from '../RootFolder/RootFolders.css';

class MagazineRootFolders extends Component {
  constructor(props, context) {
    super(props, context);

    this.state = {
      isAddRootFolderModalOpen: false
    };
  }

  onAddRootFolderPress = () => {
    this.setState({ isAddRootFolderModalOpen: true });
  };

  onAddRootFolderModalClose = () => {
    this.setState({ isAddRootFolderModalOpen: false });
  };

  render() {
    const {
      items,
      onConfirmDeleteRootFolder,
      ...otherProps
    } = this.props;

    return (
      <FieldSet legend="Magazine Root Folders">
        <PageSectionContent
          errorMessage="Unable to load magazine root folders"
          {...otherProps}
        >
          <div className={styles.rootFolders}>
            {
              items.sort(sortByName).map((item) => (
                <MagazineRootFolder
                  key={item.id}
                  {...item}
                  onConfirmDeleteRootFolder={onConfirmDeleteRootFolder}
                />
              ))
            }

            <Card
              className={styles.addRootFolder}
              onPress={this.onAddRootFolderPress}
            >
              <div className={styles.center}>
                <Icon
                  name={icons.ADD}
                  size={45}
                />
              </div>
            </Card>
          </div>

          <EditMagazineRootFolderModal
            isOpen={this.state.isAddRootFolderModalOpen}
            onModalClose={this.onAddRootFolderModalClose}
          />
        </PageSectionContent>
      </FieldSet>
    );
  }
}

MagazineRootFolders.propTypes = {
  isFetching: PropTypes.bool.isRequired,
  isPopulated: PropTypes.bool.isRequired,
  error: PropTypes.object,
  items: PropTypes.arrayOf(PropTypes.object).isRequired,
  onConfirmDeleteRootFolder: PropTypes.func.isRequired
};

export default MagazineRootFolders;
