using System;
using System.Collections.Immutable;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Threading;

namespace PeerCastStation.Core
{
  public class OperationCanceledWithArgException<T> : OperationCanceledException
  {
    public new CancellationTokenWithArg<T> CancellationToken { get; }
    public T? Value { get; }

    public OperationCanceledWithArgException(T? value)
      : this(null, null, CancellationTokenWithArg<T>.None, value)
    {
    }

    public OperationCanceledWithArgException(string? message, T? value)
      : this(message, null, CancellationTokenWithArg<T>.None, value)
    {
    }

    public OperationCanceledWithArgException(string? message, CancellationTokenWithArg<T> cancellationToken)
      : this(message, null, cancellationToken, cancellationToken.Value)
    {
    }

    public OperationCanceledWithArgException(CancellationTokenWithArg<T> cancellationToken)
      : this(null, null, cancellationToken, cancellationToken.Value)
    {
    }

    public OperationCanceledWithArgException(string? message, Exception? innerException, CancellationTokenWithArg<T> cancellationToken)
      : this(message, innerException, cancellationToken, cancellationToken.Value)

    {
    }

    public OperationCanceledWithArgException(string? message, Exception? innerException, CancellationTokenWithArg<T> cancellationToken, T? value)
      : base(message, innerException, cancellationToken.CancellationToken)
    {
      CancellationToken = cancellationToken;
      Value = value;
    }
  }

  public struct CancellationTokenWithArg<T>
  {
    public static readonly CancellationTokenWithArg<T> None = new();
    internal CancellationTokenWithArg(CancellationTokenSourceWithArg<T> source, CancellationToken cancellationToken)
    {
      Source = source;
      CancellationToken = cancellationToken;
    }
    private CancellationTokenSourceWithArg<T>? Source { get; } = default;
    public CancellationToken CancellationToken { get; } = default;
    public T? Value {
      get {
        if (Source!=null) {
          return Source.Value;
        }
        else {
          return default;
        }
      }
    }

    public bool CanBeCanceled { get { return CancellationToken.CanBeCanceled; } }
    [MemberNotNullWhen(true, "Value")]
    public bool IsCancellationRequested { get { return CancellationToken.IsCancellationRequested; } }

    public CancellationTokenRegistration Register(Action<T> cancelledAction)
    {
      var source = Source;
      if (source!=null) {
        return CancellationToken.Register(() => cancelledAction(source.Value!));
      }
      else {
        return default;
      }
    }

    public CancellationTokenRegistration Register(Action<T> cancelledAction, bool useSynchronizationContext)
    {
      var source = Source;
      if (source!=null) {
        return CancellationToken.Register(() => cancelledAction(source.Value!), useSynchronizationContext);
      }
      else {
        return default;
      }
    }

    public CancellationTokenRegistration Register(Action<object?, T> cancelledAction, object? state)
    {
      var source = Source;
      if (source!=null) {
        return CancellationToken.Register(s => cancelledAction(s, source.Value!), state);
      }
      else {
        return default;
      }
    }

    public CancellationTokenRegistration Register(Action<object?, T> cancelledAction, object? state, bool useSynchronizationContext)
    {
      var source = Source;
      if (source!=null) {
        return CancellationToken.Register(s => cancelledAction(s, source.Value!), state, useSynchronizationContext);
      }
      else {
        return default;
      }
    }

    public void ThrowIfCancellationRequested()
    {
      if (IsCancellationRequested) {
        throw new OperationCanceledWithArgException<T>(this);
      }
    }

    public static implicit operator CancellationToken(in CancellationTokenWithArg<T> self)
    {
      return self.CancellationToken;
    }

  }

  public class CancellationTokenSourceWithArg<T>
    : IDisposable
  {
    private CancellationTokenSource cancellationTokenSource = new CancellationTokenSource();
    private ImmutableArray<CancellationTokenRegistration> registrations = ImmutableArray<CancellationTokenRegistration>.Empty;
    public CancellationTokenWithArg<T> Token {
      get {
        return new CancellationTokenWithArg<T>(this, cancellationTokenSource.Token);
      }
    }
    public T? Value { get; private set; } = default;
    [MemberNotNullWhen(true, "Value")]
    public bool IsCancellationRequested { get { return cancellationTokenSource.IsCancellationRequested; } }

    public CancellationTokenSourceWithArg()
    {
    }

    private CancellationTokenSourceWithArg(params CancellationTokenWithArg<T>[] tokens)
    {
      registrations = tokens.Select(t => t.Register(v => TryCancel(v))).ToImmutableArray();
    }

    public bool TryCancel(T value)
    {
      lock (cancellationTokenSource) {
        if (!cancellationTokenSource.IsCancellationRequested) {
          Value = value;
          cancellationTokenSource.Cancel();
          return true;
        }
        else {
          return false;
        }
      }
    }

    public bool TryCancel(T value, bool throwOnFirstException)
    {
      lock (cancellationTokenSource) {
        if (!cancellationTokenSource.IsCancellationRequested) {
          Value = value;
          cancellationTokenSource.Cancel(throwOnFirstException);
          return true;
        }
        else {
          return false;
        }
      }
    }

    public void Cancel(T value)
    {
      if (!TryCancel(value)) {
        throw new InvalidOperationException("Already canceled");
      }
    }

    public void Cancel(T value, bool throwOnFirstException)
    {
      if (!TryCancel(value, throwOnFirstException)) {
        throw new InvalidOperationException("Already canceled");
      }
    }

    public void Dispose()
    {
      lock (cancellationTokenSource) {
        foreach (var reg in registrations) {
          reg.Dispose();
        }
        cancellationTokenSource.Dispose();
      }
    }

    public static CancellationTokenSourceWithArg<T> CreateLinkedTokenSource(params CancellationTokenWithArg<T>[] tokens)
    {
      return new CancellationTokenSourceWithArg<T>(tokens);
    }
  }

}
