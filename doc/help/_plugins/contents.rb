
module Jekyll
  module ContentsFilter
    def build_contents(input)
      headers = []
      input.scan(%r(<h(\d)(.*?)>(.+?)</h\d>)) do |ary|
        lv = ary[0].to_i
        md = /id=['"](.*?)['"]/.match(ary[1])
        headers << "<a class='list-group-item list-group-item-action' href='##{md[1]}'>#{ary[2]}</a>" if md and lv<=3
      end
      headers.join("\n")
    end
  end
end
Liquid::Template.register_filter(Jekyll::ContentsFilter)

