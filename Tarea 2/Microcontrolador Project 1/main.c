#include <stdio.h>
#include <stdlib.h>
#include <stdbool.h>

// Definición de estados de la máquina de portón eléctrico.
typedef enum {
    STATE_INIT,
    STATE_CLOSED,
    STATE_OPEN,
    STATE_OPENING,
    STATE_CLOSING,
    STATE_ERROR
} State;

// Constantes y errores.
#define TIME_MAX 180
#define ERROR_OK 0
#define ERROR_LIMIT 1
#define ERROR_TIME 2
#define ERROR_START 3
#define PARPADEO_LENTO 20 // Ajuste de parpadeo

// Estructura de control del sistema para el portón.
typedef struct {
    bool sensorClosed;
    bool sensorOpen;
    bool activationButton;
    bool motorOpen;
    bool motorClose;
    int timeCounter;
    bool ledOpen;
    bool ledClose;
    bool ledError;
    int errorCode;
    bool dataReady;
    bool exitFlag; // Nuevo: Permite salir del bucle
} ControlIO;

// Prototipos de funciones
State state_init(ControlIO *io);
State state_closed(ControlIO *io);
State state_open(ControlIO *io);
State state_opening(ControlIO *io);
State state_closing(ControlIO *io);
State state_error(ControlIO *io, int error);
void Timer50ms(ControlIO *io);

// Función principal
int main() {
    State currentState = STATE_INIT;
    ControlIO io = {0}; // Inicializar valores de control a 0.

    while (!io.exitFlag) {
        Timer50ms(&io);

        switch (currentState) {
            case STATE_INIT:     currentState = state_init(&io); break;
            case STATE_CLOSED:   currentState = state_closed(&io); break;
            case STATE_OPEN:     currentState = state_open(&io); break;
            case STATE_OPENING:  currentState = state_opening(&io); break;
            case STATE_CLOSING:  currentState = state_closing(&io); break;
            case STATE_ERROR:    currentState = state_error(&io, io.errorCode); break;
        }
    }
    return 0;
}

// Implementación de los estados
State state_init(ControlIO *io) {
    if (!io->dataReady) {
        io->errorCode = ERROR_START;
        return STATE_ERROR;
    }
    return io->sensorClosed ? STATE_CLOSED : STATE_OPEN;
}

State state_closed(ControlIO *io) {
    io->motorOpen = false;
    io->motorClose = false;
    io->ledOpen = false;
    io->ledClose = true;
    if (io->activationButton) {
        return STATE_OPENING;
    }
    return STATE_CLOSED;
}

State state_open(ControlIO *io) {
    io->motorOpen = false;
    io->motorClose = false;
    io->ledOpen = true;
    io->ledClose = false;
    if (io->activationButton) {
        return STATE_CLOSING;
    }
    return STATE_OPEN;
}

State state_opening(ControlIO *io) {
    io->motorOpen = true;
    io->motorClose = false;
    io->timeCounter = 0;

    while (!io->sensorOpen && io->timeCounter < TIME_MAX) {
        io->timeCounter++;
    }

    if (io->sensorOpen) {
        return STATE_OPEN;
    } else {
        io->errorCode = ERROR_TIME;
        return STATE_ERROR;
    }
}

State state_closing(ControlIO *io) {
    io->motorOpen = false;
    io->motorClose = true;
    io->timeCounter = 0;

    while (!io->sensorClosed && io->timeCounter < TIME_MAX) {
        io->timeCounter++;
    }

    if (io->sensorClosed) {
        return STATE_CLOSED;
    } else {
        io->errorCode = ERROR_TIME;
        return STATE_ERROR;
    }
}

State state_error(ControlIO *io, int error) {
    io->ledError = true;
    io->motorOpen = false;
    io->motorClose = false;

    if (error == ERROR_START) {
        io->exitFlag = true;
    }

    return STATE_ERROR;
}

void Timer50ms(ControlIO *io) {
    static int counter = 0;
    counter++;
    if (counter >= PARPADEO_LENTO) {
        io->ledError = !io->ledError;
        counter = 0;
    }
}
