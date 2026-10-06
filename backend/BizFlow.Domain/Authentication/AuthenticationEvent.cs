namespace BizFlow.Domain.Authentication;

public enum AuthenticationEvent { LoginSucceeded, LoginFailed, RefreshSucceeded, RefreshDenied, ReplayDetected, PasswordResetRequested, PasswordResetSucceeded }
