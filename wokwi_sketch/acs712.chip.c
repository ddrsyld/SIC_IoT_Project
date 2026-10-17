#include "wokwi-api.h"
#include <stdlib.h>

typedef struct {
  pin_t out_pin;
  uint32_t current_attr;
  timer_t update_timer;
} chip_state_t;

static void update_output(void *user_data) {
  chip_state_t *chip = (chip_state_t *)user_data;

  // Baca nilai slider "current" dari file .chip.json.
  float current_a = attr_read_float(chip->current_attr);

  // Pemetaan simulasi:
  // -30 A -> 0.0 V
  //   0 A -> 1.65 V
  // +30 A -> 3.3 V
float out_voltage = ((current_a + 30.0f) / 60.0f) * 3.3f;

if (out_voltage < 0.0f) out_voltage = 0.0f;
if (out_voltage > 3.3f) out_voltage = 3.3f;

pin_dac_write(chip->out_pin, out_voltage);
}

static void timer_callback(void *user_data) {
  update_output(user_data);
}

void chip_init(void) {
  chip_state_t *chip = (chip_state_t *)malloc(sizeof(chip_state_t));
  if (chip == NULL) return;

  chip->out_pin = pin_init("OUT", ANALOG);
  chip->current_attr = attr_init_float("current", 0.0f);

  const timer_config_t timer_config = {
    .callback = timer_callback,
    .user_data = chip,
  };
  chip->update_timer = timer_init((timer_config_t *)&timer_config);

  update_output(chip);
  timer_start(chip->update_timer, 1000, true);
}
