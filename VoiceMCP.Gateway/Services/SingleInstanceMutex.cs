using System.Security.AccessControl;
using System.Security.Principal;

namespace VoiceMCP.Gateway.Services;

public class SingleInstanceMutex : IDisposable
{
    private readonly Mutex? _mutex;
    private readonly bool _createdNew;
    
    public SingleInstanceMutex()
    {
        try
        {
            var mutexName = @"Global\VoiceMCP-Gateway";
            var allowEveryoneRule = new MutexAccessRule(
                new SecurityIdentifier(WellKnownSidType.WorldSid, null),
                MutexRights.FullControl,
                AccessControlType.Allow);
            
            var securitySettings = new MutexSecurity();
            securitySettings.AddAccessRule(allowEveryoneRule);
            
            _mutex = MutexAcl.Create(false, mutexName, out _createdNew, securitySettings);
            
            if (!_createdNew && _mutex != null && !_mutex.WaitOne(0))
            {
                throw new InvalidOperationException("Another instance of VoiceMCP Gateway is already running. Only one instance is allowed.");
            }
        }
        catch (Exception ex) when (ex is not InvalidOperationException)
        {
            throw new InvalidOperationException($"Failed to create singleton mutex: {ex.Message}", ex);
        }
    }
    
    public void Dispose()
    {
        if (_createdNew && _mutex != null)
        {
            _mutex.ReleaseMutex();
            _mutex.Dispose();
        }
    }
}
