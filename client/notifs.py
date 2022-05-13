from winotify import Notification
from sys import argv as args

import argparse

parser = argparse.ArgumentParser(description="Process Notification Arguments")

parser.add_argument("--app_id", metavar='I', type=str, help="who the toast is being sent by", default="ClientNotifier")
parser.add_argument("--title", metavar='T', type=str, help="toast title", default="DChat Client")
parser.add_argument("--msg", metavar='M', type=str, help="message", default="no message provided")
parser.add_argument("--dur", metavar='d', type=str, help="set how long the toast will show for (short|long)", default="short")


result = parser.parse_args()

appid = result.app_id
title = result.title
message = result.msg
dur = result.dur


toast = Notification(
    app_id=appid,
    title=title,
    msg=message,
    duration=dur
)

toast.show()
