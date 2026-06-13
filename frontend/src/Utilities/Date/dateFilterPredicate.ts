import moment from 'moment';
import * as filterTypes from 'Helpers/Props/filterTypes';
import isAfter from 'Utilities/Date/isAfter';
import isBefore from 'Utilities/Date/isBefore';

interface FilterValue {
  time: string;
  value: number;
}

export default function dateFilterPredicate(
  itemValue: string | null | undefined,
  filterValue: string | FilterValue,
  type: string
): boolean {
  if (!itemValue) {
    return false;
  }

  switch (type) {
    case filterTypes.LESS_THAN:
      return moment(itemValue).isBefore(filterValue as string);

    case filterTypes.GREATER_THAN:
      return moment(itemValue).isAfter(filterValue as string);

    case filterTypes.IN_LAST: {
      const fv = filterValue as FilterValue;
      return (
        isAfter(itemValue, { [fv.time]: fv.value * -1 }) && isBefore(itemValue)
      );
    }

    case filterTypes.NOT_IN_LAST: {
      const fv = filterValue as FilterValue;
      return isBefore(itemValue, { [fv.time]: fv.value * -1 });
    }

    case filterTypes.IN_NEXT: {
      const fv = filterValue as FilterValue;
      return isAfter(itemValue) && isBefore(itemValue, { [fv.time]: fv.value });
    }

    case filterTypes.NOT_IN_NEXT: {
      const fv = filterValue as FilterValue;
      return isAfter(itemValue, { [fv.time]: fv.value });
    }

    default:
      return false;
  }
}
