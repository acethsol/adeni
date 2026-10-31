namespace Adeni.Domain.Booking;

/// <summary>Derives work state and validates attendance event transitions (Phase A — no geofence).</summary>
public static class StaffAttendanceStateMachine
{
    public static string DeriveState(IReadOnlyList<string> eventTypesInOrder)
    {
        var state = StaffAttendanceWorkStates.NotWorking;
        foreach (var type in eventTypesInOrder)
        {
            if (!StaffAttendanceEventTypes.All.Contains(type))
            {
                continue;
            }

            state = type switch
            {
                StaffAttendanceEventTypes.ClockIn => StaffAttendanceWorkStates.Working,
                StaffAttendanceEventTypes.BreakStart when state == StaffAttendanceWorkStates.Working
                    => StaffAttendanceWorkStates.OnBreak,
                StaffAttendanceEventTypes.BreakEnd when state == StaffAttendanceWorkStates.OnBreak
                    => StaffAttendanceWorkStates.Working,
                StaffAttendanceEventTypes.ClockOut => StaffAttendanceWorkStates.NotWorking,
                _ => state,
            };
        }

        return state;
    }

    public static bool CanApply(string currentState, string eventType) =>
        eventType switch
        {
            StaffAttendanceEventTypes.ClockIn => currentState == StaffAttendanceWorkStates.NotWorking,
            StaffAttendanceEventTypes.BreakStart => currentState == StaffAttendanceWorkStates.Working,
            StaffAttendanceEventTypes.BreakEnd => currentState == StaffAttendanceWorkStates.OnBreak,
            StaffAttendanceEventTypes.ClockOut => currentState is StaffAttendanceWorkStates.Working or StaffAttendanceWorkStates.OnBreak,
            _ => false,
        };
}
