# ChatApplication

A TCP chat application with broadcast and private messaging channels. Clients can also request a current stock value from the broadcast channel with `stock <ticker>`.

## Prerequisites

- .NET SDK with C# 10.0 or later
- Internet access for the `stock <ticker>` command

## Launching

Open a terminal in the repository root.

### 1. Run the server once

```bash
dotnet run --project Server
```

At the server prompt, enter:

```text
start
```

The server listens on `127.0.0.1:5000`.

### 2. Run as many clients as needed

Open a separate terminal for each client and run:

```bash
dotnet run --project Client
```

Enter a unique username when prompted. Start additional clients in additional terminals.

## Client commands

- `/broadcast` - Switch to the broadcast channel.
- `/private <username>` - Switch to a private channel with another connected user.
- `stock <ticker>` - Fetch and broadcast the current stock value while in the broadcast channel, for example:

  ```text
  stock AAPL
  ```

## Contact

[pablo.ferreiraa10@gmail.com](mailto:pablo.ferreiraa10@gmail.com)
