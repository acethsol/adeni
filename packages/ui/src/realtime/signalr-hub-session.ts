type HubConnection = import("@microsoft/signalr").HubConnection;
type HubConnectionState = typeof import("@microsoft/signalr").HubConnectionState;

export type SignalRHubHandler = (payload: unknown) => void;

export type SignalRHubSessionOptions = {
  hubUrl: string;
  accessTokenFactory?: () => Promise<string>;
  handlers: Record<string, SignalRHubHandler>;
  onReconnected?: () => Promise<void>;
};

/** One SignalR hub connection with lazy @microsoft/signalr import. */
export class SignalRHubSession {
  private connection: HubConnection | null = null;
  private hubStates: HubConnectionState | null = null;

  isConnected(): boolean {
    if (!this.connection || !this.hubStates) {
      return false;
    }
    return this.connection.state === this.hubStates.Connected;
  }

  async connect(options: SignalRHubSessionOptions): Promise<void> {
    const signalr = await import("@microsoft/signalr");
    this.hubStates = signalr.HubConnectionState;

    if (this.connection?.state === signalr.HubConnectionState.Connected) {
      return;
    }

    await this.disconnect();

    const builder = new signalr.HubConnectionBuilder()
      .withUrl(options.hubUrl, {
        accessTokenFactory: options.accessTokenFactory ?? (async () => ""),
      })
      .withAutomaticReconnect([0, 2000, 5000, 10000])
      .configureLogging(signalr.LogLevel.Warning);

    const connection = builder.build();
    for (const [event, handler] of Object.entries(options.handlers)) {
      connection.on(event, handler);
    }
    if (options.onReconnected) {
      connection.onreconnected(() => {
        void options.onReconnected?.();
      });
    }

    this.connection = connection;
    try {
      await connection.start();
    } catch (error) {
      this.connection = null;
      try {
        await connection.stop();
      } catch {
        // ignore
      }
      throw error;
    }
  }

  async invoke(method: string, ...args: unknown[]): Promise<void> {
    const connected = this.hubStates?.Connected;
    if (!this.connection || connected == null || this.connection.state !== connected) {
      return;
    }
    await this.connection.invoke(method, ...args);
  }

  async disconnect(): Promise<void> {
    const connection = this.connection;
    this.connection = null;
    if (!connection) {
      return;
    }
    try {
      await connection.stop();
    } catch {
      // ignore teardown failures
    }
  }
}
