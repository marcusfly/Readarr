function formatAge(
  age: number | string,
  ageHours: number | string,
  ageMinutes?: number | string | null
): string {
  const roundedAge = Math.round(Number(age));
  const parsedHours = parseFloat(String(ageHours));
  const parsedMinutes = ageMinutes ? parseFloat(String(ageMinutes)) : null;

  if (roundedAge < 2 && parsedHours) {
    if (parsedHours < 2 && parsedMinutes != null) {
      return `${parsedMinutes.toFixed(0)} ${
        parsedHours === 1 ? 'minute' : 'minutes'
      }`;
    }

    return `${parsedHours.toFixed(1)} ${parsedHours === 1 ? 'hour' : 'hours'}`;
  }

  return `${roundedAge} ${roundedAge === 1 ? 'day' : 'days'}`;
}

export default formatAge;
