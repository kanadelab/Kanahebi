
events = {}

def request(req):
	lines = req.split("\r\n")
	values = {}
	for line in lines:
		sp = line.split(": ", 2)
		if len(sp) == 2:
			values[sp[0]] = sp[1]

	if "Event" in values:
		if values["Event"] in events:
			response = events[values["Event"]](values)
			return "SHIORI/2.2 200 OK\r\nCharset: Shift_JIS\r\nSentence: %s\r\n\r\n" % (response)
	return "SHIORI/2.2 204 No Content\r\n\r\n"
	
events["OnBoot"] = lambda values: "\\0\\s[5]起動しちゃったねー。\\e"
events["OnMouseDoubleClick"] = lambda values: "\\0\\s[20]つつくなよお。\\e"
