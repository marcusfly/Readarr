import moment from 'moment';

function isSameWeek(date: string | null | undefined): boolean {
  if (!date) {
    return false;
  }

  return moment(date).isSame(moment(), 'week');
}

export default isSameWeek;
