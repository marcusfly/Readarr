import moment from 'moment';

function isAfter(
  date: string | null | undefined,
  offsets: Record<string, number> = {}
): boolean {
  if (!date) {
    return false;
  }

  const offsetTime = moment();

  Object.keys(offsets).forEach((key) => {
    offsetTime.add(offsets[key], key as moment.unitOfTime.DurationConstructor);
  });

  return moment(date).isAfter(offsetTime);
}

export default isAfter;
