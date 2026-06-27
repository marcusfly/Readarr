import { createAction } from 'redux-actions';
import { createThunk, handleThunks } from 'Store/thunks';
import createFetchHandler from './Creators/createFetchHandler';
import createHandleActions from './Creators/createHandleActions';
import createRemoveItemHandler from './Creators/createRemoveItemHandler';
import createSaveProviderHandler from './Creators/createSaveProviderHandler';
import createSetSettingValueReducer from './Creators/Reducers/createSetSettingValueReducer';

export const section = 'magazines';

export const defaultState = {
  isFetching: false,
  isPopulated: false,
  isSaving: false,
  isDeleting: false,
  error: null,
  saveError: null,
  deleteError: null,
  pendingChanges: {},
  items: []
};

export const FETCH_MAGAZINES = 'magazines/fetchMagazines';
export const SET_MAGAZINE_VALUE = 'magazines/setMagazineValue';
export const SAVE_MAGAZINE = 'magazines/saveMagazine';
export const DELETE_MAGAZINE = 'magazines/deleteMagazine';

export const fetchMagazines = createThunk(FETCH_MAGAZINES);
export const saveMagazine = createThunk(SAVE_MAGAZINE);
export const deleteMagazine = createThunk(DELETE_MAGAZINE, (payload) => {
  return {
    ...payload,
    queryParams: {
      deleteFiles: payload.deleteFiles
    }
  };
});
export const setMagazineValue = createAction(SET_MAGAZINE_VALUE, (payload) => {
  return {
    section,
    ...payload
  };
});

export const actionHandlers = handleThunks({
  [FETCH_MAGAZINES]: createFetchHandler(section, '/magazine'),
  [SAVE_MAGAZINE]: createSaveProviderHandler(section, '/magazine'),
  [DELETE_MAGAZINE]: createRemoveItemHandler(section, '/magazine')
});

export const reducers = createHandleActions({
  [SET_MAGAZINE_VALUE]: createSetSettingValueReducer(section)
}, defaultState, section);
