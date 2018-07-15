
module Jekyll
  module ContentsFilter
    def build_contents(input)
      headers = []
      input.scan(%r(<h(\d)(.*?)>(.+?)</h\d>)) do |ary|
        lv = ary[0].to_i
        md = /id=['"](.*?)['"]/.match(ary[1])
        headers << "<li><a href='##{md[1]}'>#{ary[2]}</a></li>" if md and lv<=3
      end
      headers.join("\n")
    end
  end
end
Liquid::Template.register_filter(Jekyll::ContentsFilter)

