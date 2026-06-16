import { batchActions } from 'redux-batched-actions';
import { createThunk, handleThunks } from 'Store/thunks';
import createAjaxRequest from 'Utilities/createAjaxRequest';
import { set, updateItem } from './baseActions';
import createFetchHandler from './Creators/createFetchHandler';
import createHandleActions from './Creators/createHandleActions';

export const section = 'magazineIssues';

export const defaultState = {
  isFetching: false,
  isPopulated: false,
  error: null,
  items: []
};

export const FETCH_MAGAZINE_ISSUES = 'magazineIssues/fetchMagazineIssues';
export const TOGGLE_MAGAZINE_ISSUE_MONITORED = 'magazineIssues/toggleMagazineIssueMonitored';

export const fetchMagazineIssues = createThunk(FETCH_MAGAZINE_ISSUES);
export const toggleMagazineIssueMonitored = createThunk(TOGGLE_MAGAZINE_ISSUE_MONITORED);

export const actionHandlers = handleThunks({
  [FETCH_MAGAZINE_ISSUES]: createFetchHandler(section, '/magazineissue'),

  [TOGGLE_MAGAZINE_ISSUE_MONITORED]: function(getState, payload, dispatch) {
    dispatch(set({ section, isFetching: true }));

    const { request } = createAjaxRequest({
      url: '/magazineissue/monitor',
      method: 'PUT',
      dataType: 'json',
      data: JSON.stringify({
        issueIds: [payload.issueId],
        monitored: payload.monitored
      })
    });

    request.done((data) => {
      const updates = data.map((issue) => updateItem({ section, ...issue, updateOnly: true }));

      dispatch(batchActions([
        ...updates,
        set({
          section,
          isFetching: false,
          isPopulated: true,
          error: null
        })
      ]));
    });

    request.fail((xhr) => {
      dispatch(set({
        section,
        isFetching: false,
        error: xhr.aborted ? null : xhr
      }));
    });
  }
});

export const reducers = createHandleActions({}, defaultState, section);
