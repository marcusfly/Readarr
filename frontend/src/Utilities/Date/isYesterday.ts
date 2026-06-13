import moment from 'moment';

function isYesterday(date: string | null | undefined): boolean {
  if (!date) {
    return false;
  }

  return moment(date).isSame(moment().subtract(1, 'day'), 'day');
}

export default isYesterday;
