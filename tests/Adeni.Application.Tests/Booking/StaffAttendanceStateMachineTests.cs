namespace Adeni.Application.Tests.Booking;

using Adeni.Domain.Booking;

public sealed class StaffAttendanceStateMachineTests
{
    [Fact]
    public void DeriveState_clockIn_break_clockOut()
    {
        var state = StaffAttendanceStateMachine.DeriveState(
        [
            StaffAttendanceEventTypes.ClockIn,
            StaffAttendanceEventTypes.BreakStart,
            StaffAttendanceEventTypes.BreakEnd,
            StaffAttendanceEventTypes.ClockOut,
        ]);
        Assert.Equal(StaffAttendanceWorkStates.NotWorking, state);
    }

    [Theory]
    [InlineData(StaffAttendanceWorkStates.NotWorking, StaffAttendanceEventTypes.ClockIn, true)]
    [InlineData(StaffAttendanceWorkStates.Working, StaffAttendanceEventTypes.BreakStart, true)]
    [InlineData(StaffAttendanceWorkStates.OnBreak, StaffAttendanceEventTypes.BreakEnd, true)]
    [InlineData(StaffAttendanceWorkStates.Working, StaffAttendanceEventTypes.ClockOut, true)]
    [InlineData(StaffAttendanceWorkStates.NotWorking, StaffAttendanceEventTypes.ClockOut, false)]
    [InlineData(StaffAttendanceWorkStates.Working, StaffAttendanceEventTypes.ClockIn, false)]
    public void CanApply_respects_state(string state, string eventType, bool expected) =>
        Assert.Equal(expected, StaffAttendanceStateMachine.CanApply(state, eventType));
}
