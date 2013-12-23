
module Jekyll
  module ReleasesizeFilter
    ReleasePath = File.join(File.dirname(__FILE__), '..', '..', 'releases')
    def releasesize(input)
      File.size(File.join(ReleasePath, "PeerCastStation-#{input}.zip")).to_s
    end
  end
end
Liquid::Template.register_filter(Jekyll::ReleasesizeFilter)

