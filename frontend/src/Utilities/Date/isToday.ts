import moment from 'moment';

function isToday(date: string | null | undefined): boolean {
  if (!date) {
    return false;
  }

  return moment(date).isSame(moment(), 'day');
}

export default isToday;
