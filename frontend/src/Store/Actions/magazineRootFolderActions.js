import { createAction } from 'redux-actions';
import { createThunk, handleThunks } from 'Store/thunks';
import monitorOptions from 'Utilities/Magazine/monitorOptions';
import createFetchHandler from './Creators/createFetchHandler';
import createHandleActions from './Creators/createHandleActions';
import createRemoveItemHandler from './Creators/createRemoveItemHandler';
import createSaveProviderHandler from './Creators/createSaveProviderHandler';
import createSetSettingValueReducer from './Creators/Reducers/createSetSettingValueReducer';

export const section = 'magazineRootFolders';

export const defaultState = {
  isFetching: false,
  isPopulated: false,
  error: null,
  isSaving: false,
  saveError: null,
  isDeleting: false,
  deleteError: null,
  items: [],
  pendingChanges: {},
  schema: {
    name: '',
    path: '',
    defaultQualityProfileId: 0,
    defaultMetadataProfileId: 0,
    defaultMonitorOption: monitorOptions[0].key,
    defaultTags: []
  }
};

export const FETCH_MAGAZINE_ROOT_FOLDERS = 'magazineRootFolders/fetchMagazineRootFolders';
export const SAVE_MAGAZINE_ROOT_FOLDER = 'magazineRootFolders/saveMagazineRootFolder';
export const DELETE_MAGAZINE_ROOT_FOLDER = 'magazineRootFolders/deleteMagazineRootFolder';
export const SET_MAGAZINE_ROOT_FOLDER_VALUE = 'magazineRootFolders/setMagazineRootFolderValue';

export const fetchMagazineRootFolders = createThunk(FETCH_MAGAZINE_ROOT_FOLDERS);
export const saveMagazineRootFolder = createThunk(SAVE_MAGAZINE_ROOT_FOLDER);
export const deleteMagazineRootFolder = createThunk(DELETE_MAGAZINE_ROOT_FOLDER);

export const setMagazineRootFolderValue = createAction(SET_MAGAZINE_ROOT_FOLDER_VALUE, (payload) => {
  return {
    section,
    ...payload
  };
});

export const actionHandlers = handleThunks({
  [FETCH_MAGAZINE_ROOT_FOLDERS]: createFetchHandler(section, '/magazinerootfolder'),
  [SAVE_MAGAZINE_ROOT_FOLDER]: createSaveProviderHandler(section, '/magazinerootfolder'),
  [DELETE_MAGAZINE_ROOT_FOLDER]: createRemoveItemHandler(section, '/magazinerootfolder')
});

export const reducers = createHandleActions({
  [SET_MAGAZINE_ROOT_FOLDER_VALUE]: createSetSettingValueReducer(section)
}, defaultState, section);
