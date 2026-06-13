import moment from 'moment';

function isBefore(
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

  return moment(date).isBefore(offsetTime);
}

export default isBefore;
