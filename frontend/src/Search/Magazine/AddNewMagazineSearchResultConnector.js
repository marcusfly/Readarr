import { connect } from 'react-redux';
import { createSelector } from 'reselect';
import createDimensionsSelector from 'Store/Selectors/createDimensionsSelector';
import AddNewMagazineSearchResult from './AddNewMagazineSearchResult';

function createMapStateToProps() {
  return createSelector(
    (state, props) => props.id,
    createDimensionsSelector(),
    (id, dimensions) => {
      return {
        isExistingMagazine: id !== 0,
        isSmallScreen: dimensions.isSmallScreen
      };
    }
  );
}

export default connect(createMapStateToProps)(AddNewMagazineSearchResult);
