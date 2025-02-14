#include <stdio.h>
#include "freertos/FreeRTOS.h"
#include "freertos/task.h"
#include "driver/gpio.h"
#include "esp_log.h"

// Macros para facilitar el entendimiento del código
#define ESTADO_INIT 0
#define ESTADO_CERRADO 1
#define ESTADO_ABIERTO 2
#define ESTADO_CERRANDO 3
#define ESTADO_ABRIENDO 4
#define ESTADO_ERROR 5
#define TRUE 1
#define FALSE 0
#define RT_MAX 180 // Tiempo máximo en segundos
#define ERROR_OK 0
#define ERROR_LS 1
#define ERROR_RT 2
#define ERROR_OBSTACULO 3 // Nuevo código de error para obstáculos

// Tags para logging
static const char* TAG = "PuertaAutomatica";

// Definición de pines GPIO
#define LSC_GPIO GPIO_NUM_4      // Limit switch cerrado
#define LSA_GPIO GPIO_NUM_5      // Limit switch abierto
#define SPP_GPIO GPIO_NUM_6      // Comando pulso-pulso (botón)
#define MA_GPIO GPIO_NUM_7       // Motor abrir
#define MC_GPIO GPIO_NUM_8       // Motor cerrar
#define LED_A_GPIO GPIO_NUM_9    // Led abriendo
#define LED_C_GPIO GPIO_NUM_10   // Led cerrando
#define LED_ER_GPIO GPIO_NUM_11  // Led error
#define SENSOR_OBSTACULO_GPIO GPIO_NUM_12 // Sensor de obstáculos
#define BUZZER_GPIO GPIO_NUM_13  // Buzzer para alerta de obstáculo

// Estructura de datos para organizar y almacenar las variables
struct DATA_IO
{
    unsigned int LSC:1;       // Limit switch cerrado
    unsigned int LSA:1;       // Limit switch abierto
    unsigned int SPP:1;       // Comando pulso-pulso
    unsigned int MA:1;        // Motor abrir
    unsigned int MC:1;        // Motor cerrar
    unsigned int Cont_RT;     // Contador Run Time en segundos
    unsigned int Led_A:1;     // Led abriendo
    unsigned int Led_C:1;     // Led cerrando
    unsigned int Led_ER:1;    // Led error
    unsigned int COD_ERR;     // Código de error
    unsigned int DATOS_READY:1; // Confirmación de recepción de datos
    unsigned int Obstaculo:1; // Detección de obstáculo
}
data_io;

// Prototipos de funciones
void configurar_gpios();
int Funcion_INIT();
int Funcion_ABIERTO();
int Funcion_ABRIENDO();
int Funcion_CERRADO();
int Funcion_CERRANDO();
int Funcion_ERROR();

// Función principal
void app_main()
{
    // Configurar GPIOs
    configurar_gpios();

    // Inicializar la máquina de estados
    int ESTADO_SIGUIENTE = ESTADO_INIT;

    while (1)
    {
        switch (ESTADO_SIGUIENTE)
        {
            case ESTADO_INIT:
                ESTADO_SIGUIENTE = Funcion_INIT();
                break;
            case ESTADO_ABIERTO:
                ESTADO_SIGUIENTE = Funcion_ABIERTO();
                break;
            case ESTADO_ABRIENDO:
                ESTADO_SIGUIENTE = Funcion_ABRIENDO();
                break;
            case ESTADO_CERRADO:
                ESTADO_SIGUIENTE = Funcion_CERRADO();
                break;
            case ESTADO_CERRANDO:
                ESTADO_SIGUIENTE = Funcion_CERRANDO();
                break;
            case ESTADO_ERROR:
                ESTADO_SIGUIENTE = Funcion_ERROR();
                break;
            default:
                ESP_LOGE(TAG, "Estado no válido");
                break;
        }
        vTaskDelay(pdMS_TO_TICKS(100)); // Esperar 100 ms
    }
}

// Configuración de GPIOs
void configurar_gpios()
{
    gpio_config_t io_conf;

    // Configurar entradas (LSC, LSA, SPP, SENSOR_OBSTACULO)
    io_conf.mode = GPIO_MODE_INPUT;
    io_conf.pin_bit_mask = (1ULL << LSC_GPIO) | (1ULL << LSA_GPIO) | (1ULL << SPP_GPIO) | (1ULL << SENSOR_OBSTACULO_GPIO);
io_conf.intr_type = GPIO_INTR_DISABLE;
io_conf.pull_up_en = GPIO_PULLUP_ENABLE;
gpio_config(&io_conf);

// Configurar salidas (MA, MC, LEDs, BUZZER)
io_conf.mode = GPIO_MODE_OUTPUT;
io_conf.pin_bit_mask = (1ULL << MA_GPIO) | (1ULL << MC_GPIO) | (1ULL << LED_A_GPIO) | (1ULL << LED_C_GPIO) | (1ULL << LED_ER_GPIO) | (1ULL << BUZZER_GPIO);
io_conf.intr_type = GPIO_INTR_DISABLE;
io_conf.pull_up_en = GPIO_PULLUP_DISABLE;
gpio_config(&io_conf);
}

// Funciones de estado (similares a las originales, pero adaptadas)
int Funcion_INIT()
{
    ESP_LOGI(TAG, "Estado: INIT");
    // Inicializar variables y GPIOs
    data_io.MA = FALSE;
    data_io.MC = FALSE;
    data_io.SPP = FALSE;
    data_io.Led_A = FALSE;
    data_io.Led_C = FALSE;
    data_io.Led_ER = FALSE;
    data_io.COD_ERR = ERROR_OK;
    data_io.Cont_RT = 0;
    data_io.DATOS_READY = TRUE;
    data_io.Obstaculo = FALSE;

    // Leer GPIOs
    data_io.LSC = gpio_get_level(LSC_GPIO);
    data_io.LSA = gpio_get_level(LSA_GPIO);

    if (data_io.LSC && !data_io.LSA) return ESTADO_CERRADO;
    if (!data_io.LSC && data_io.LSA) return ESTADO_ABRIENDO;
    if (data_io.LSC && data_io.LSA)
    {
        data_io.COD_ERR = ERROR_LS;
        return ESTADO_ERROR;
    }
    return ESTADO_CERRANDO;
}

int Funcion_CERRANDO()
{
    ESP_LOGI(TAG, "Estado: CERRANDO");
    data_io.MC = TRUE;
    data_io.Led_C = TRUE;
    data_io.Led_A = FALSE;
    data_io.Led_ER = FALSE;

    while (1)
    {
        // Leer el sensor de obstáculos
        data_io.Obstaculo = gpio_get_level(SENSOR_OBSTACULO_GPIO);

        // Si hay un obstáculo, detener el motor y activar la alerta
        if (data_io.Obstaculo)
        {
            data_io.MC = FALSE;
            data_io.Led_ER = TRUE;
            gpio_set_level(BUZZER_GPIO, TRUE); // Activar el buzzer
            ESP_LOGE(TAG, "¡Obstáculo detectado! La puerta se detiene.");
            data_io.COD_ERR = ERROR_OBSTACULO;
            return ESTADO_ERROR;
        }

        // Si la puerta está completamente cerrada
        if (gpio_get_level(LSC_GPIO))
        {
            data_io.MC = FALSE;
            data_io.Led_C = FALSE;
            return ESTADO_CERRADO;
        }

        // Si se excede el tiempo máximo
        if (data_io.Cont_RT > RT_MAX)
        {
            data_io.COD_ERR = ERROR_RT;
            return ESTADO_ERROR;
        }

        vTaskDelay(pdMS_TO_TICKS(100)); // Esperar 100 ms
        data_io.Cont_RT++;
    }
}

int Funcion_ERROR()
{
    ESP_LOGI(TAG, "Estado: ERROR");
    data_io.MA = FALSE;
    data_io.MC = FALSE;
    data_io.Led_A = FALSE;
    data_io.Led_C = FALSE;
    data_io.Led_ER = TRUE;

    // Activar el buzzer si el error es por obstáculo
    if (data_io.COD_ERR == ERROR_OBSTACULO)
    {
        gpio_set_level(BUZZER_GPIO, TRUE);
    }

    // Mensajes de error
    switch (data_io.COD_ERR)
    {
        case ERROR_LS:
            ESP_LOGE(TAG, "Error: Limit Switch defectuoso.");
            break;
        case ERROR_RT:
            ESP_LOGE(TAG, "Error: Tiempo máximo excedido.");
            break;
        case ERROR_OBSTACULO:
            ESP_LOGE(TAG, "Error: Obstáculo detectado.");
            break;
        default:
            ESP_LOGE(TAG, "Error desconocido.");
            break;
    }

    // Esperar a que el usuario presione el botón para reiniciar
    while (!gpio_get_level(SPP_GPIO))
    {
        vTaskDelay(pdMS_TO_TICKS(100));
    }

    // Reiniciar el sistema
    gpio_set_level(BUZZER_GPIO, FALSE); // Apagar el buzzer
    return ESTADO_INIT;
}
