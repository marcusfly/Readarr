import { createThunk, handleThunks } from 'Store/thunks';
import createFetchHandler from './Creators/createFetchHandler';
import createHandleActions from './Creators/createHandleActions';

export const section = 'magazines';

export const defaultState = {
  isFetching: false,
  isPopulated: false,
  error: null,
  items: []
};

export const FETCH_MAGAZINES = 'magazines/fetchMagazines';

export const fetchMagazines = createThunk(FETCH_MAGAZINES);

export const actionHandlers = handleThunks({
  [FETCH_MAGAZINES]: createFetchHandler(section, '/magazine')
});

export const reducers = createHandleActions({}, defaultState, section);
