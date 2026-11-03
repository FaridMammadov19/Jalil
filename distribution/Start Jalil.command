#!/bin/zsh

cd -- "$(dirname -- "$0")" || exit 1
./Jalil
jalil_status=$?

if [ "$jalil_status" -ne 0 ]; then
  echo
  echo "Jalil stopped with an error. Press any key to close this window."
  read -k 1
fi

exit "$jalil_status"
