from datetime import date, datetime, time, timedelta, timezone

from schemas.scheduling_validation import ExistingFarmTask, ExistingIrrigation


class SchedulingTools:
    """Pure, allow-listed scheduling checks. These helpers never write application data."""

    @staticmethod
    def date_window(start: date, end: date) -> tuple[datetime, datetime]:
        return (
            datetime.combine(start, time.min, tzinfo=timezone.utc),
            datetime.combine(end, time.max, tzinfo=timezone.utc),
        )

    @staticmethod
    def next_task_slot(
        start: datetime,
        end: datetime,
        assigned_to_user_id,
        existing: list[ExistingFarmTask],
    ) -> datetime | None:
        candidate = start
        occupied = {
            item.due_at.astimezone(timezone.utc)
            for item in existing
            if item.assigned_to_user_id == assigned_to_user_id and item.status not in {4, 6, 7}
        }
        while candidate <= end:
            if candidate not in occupied:
                return candidate
            candidate += timedelta(hours=1)
        return None

    @staticmethod
    def next_irrigation_slot(
        start: datetime,
        end: datetime,
        field_id,
        duration_minutes: int,
        existing: list[ExistingIrrigation],
    ) -> datetime | None:
        candidate = start
        duration = timedelta(minutes=duration_minutes)
        active = [item for item in existing if item.field_id == field_id and item.status not in {3, 5, 6}]
        while candidate + duration <= end:
            candidate_end = candidate + duration
            if all(
                not (
                    item.scheduled_at.astimezone(timezone.utc) < candidate_end
                    and item.scheduled_at.astimezone(timezone.utc) + timedelta(minutes=item.duration_minutes) > candidate
                )
                for item in active
            ):
                return candidate
            candidate += timedelta(hours=1)
        return None
