import moment from 'moment';

function isTomorrow(date: string | null | undefined): boolean {
  if (!date) {
    return false;
  }

  return moment(date).isSame(moment().add(1, 'day'), 'day');
}

export default isTomorrow;
