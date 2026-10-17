#include "wokwi-api.h"
#include <stdlib.h>

typedef struct {
  pin_t out;
  uint32_t voltage_attr;
  timer_t timer;
} chip_state_t;

static void update_output(void *user_data) {
  chip_state_t *chip = (chip_state_t *)user_data;

  float input_voltage =
      attr_read_float(chip->voltage_attr);

  // Batasi input 0-25 V
  if (input_voltage < 0.0f) input_voltage = 0.0f;
  if (input_voltage > 25.0f) input_voltage = 25.0f;

  // Konversi menjadi output 0-3.3 V
  float output_voltage = input_voltage * (3.3f / 25.0f);

  pin_dac_write(chip->out, output_voltage);

  timer_start(chip->timer, 1000, false);
}

void chip_init(void) {
  chip_state_t *chip = malloc(sizeof(chip_state_t));

  chip->out = pin_init("S", ANALOG);
  chip->voltage_attr = attr_init_float("voltage", 0.0f);

  const timer_config_t config = {
    .callback = update_output,
    .user_data = chip
  };

  chip->timer = timer_init(&config);

  update_output(chip);
}
